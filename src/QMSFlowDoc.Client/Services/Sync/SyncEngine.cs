using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

public class SyncEngine
{
    private readonly SnapshotStore _store;
    private readonly IDriveStorageProvider _drive;
    private readonly ISyncLogger _logger;
    private readonly IAuditLogger _audit;
    private readonly string _localRootPath;

    public event Action<string>? SyncStatusChanged;
    
    public string? DriveFolderId { get; set; }
    
    // Exponer SnapshotStore para SyncScheduler
    public SnapshotStore GetSnapshotStore() => _store;

    public SyncEngine(SnapshotStore store, IDriveStorageProvider drive, ISyncLogger logger, IAuditLogger audit, string localRootPath)
    {
        _store = store;
        _drive = drive;
        _logger = logger;
        _audit = audit;
        _localRootPath = localRootPath;
    }

    public async Task RunSyncAsync()
    {
        // Skip sync if no Drive folder is configured
        if (string.IsNullOrWhiteSpace(DriveFolderId))
        {
            await LogAsync("Sincronización omitida - No se ha configurado el ID de carpeta de Drive.");
            return;
        }

        await LogAsync("Iniciando sincronización...");
        try 
        {
            await _drive.ConnectAsync();

            // 1. Get Snapshots
            var snapshots = await _store.GetAllSnapshotsAsync();
            var snapshotDict = snapshots.ToDictionary(s => s.RelativePath, s => s);

            // 2. Scan Local
            await LogAsync("Escaneando archivos locales...");
            var localFiles = ScanLocalFiles();

            // 3. Scan Change in Local (Compare to Snapshot)
            foreach (var local in localFiles)
            {
                if (snapshotDict.TryGetValue(local.Key, out var snap))
                {
                    // Level 1 Check
                    if (local.Value.LastWriteTimeUtc != snap.LastModifiedLocalUtc || local.Value.Length != snap.SizeBytesLocal)
                    {
                        // Level 2 Check (Hash) - Only if size/time changed
                        var currentHash = await Hasher.CalculateSha256Async(Path.Combine(_localRootPath, local.Key));
                        if (currentHash != snap.Sha256Hash)
                        {
                            await LogAsync($"Cambio local detectado: {local.Key}");
                            snap.Status = SyncStatus.PendingUpload;
                            snap.Sha256Hash = currentHash;
                            snap.LastModifiedLocalUtc = local.Value.LastWriteTimeUtc;
                            snap.SizeBytesLocal = local.Value.Length;
                            await _store.UpsertSnapshotAsync(snap);
                            
                            // Audit Event
                            await _audit.LogEventAsync("LOCAL_CHANGE_DETECTED", "System", local.Key, "File modified locally.");
                        }
                    }
                }
                else
                {
                    // New Local File
                    await LogAsync($"Nuevo archivo local: {local.Key}");
                    var newSnap = new FileSnapshot
                    {
                        RelativePath = local.Key,
                        SizeBytesLocal = local.Value.Length,
                        LastModifiedLocalUtc = local.Value.LastWriteTimeUtc,
                        Sha256Hash = await Hasher.CalculateSha256Async(Path.Combine(_localRootPath, local.Key)),
                        Status = SyncStatus.PendingUpload
                    };
                    await _store.UpsertSnapshotAsync(newSnap);
                    await _audit.LogEventAsync("NEW_LOCAL_FILE", "System", local.Key, "New file created locally.");
                }
            }

            // 4. Scan Remote (Effective Reconciliation)
            await LogAsync("Escaneando archivos remotos...");
            var remoteFiles = await _drive.ListAllFilesAsync(DriveFolderId!); 
            
            var mapper = new RemoteTreeMapper(remoteFiles);

            foreach (var remote in remoteFiles)
            {
                if (remote.IsFolder) continue; 

                var relativePath = mapper.BuildRelativePath(remote.Id, DriveFolderId!);
                if (string.IsNullOrEmpty(relativePath)) continue;

                if (snapshotDict.TryGetValue(relativePath, out var snap))
                {
                    // Existing known file
                    if (snap.LastModifiedCloudUtc != remote.ModifiedTime || snap.SizeBytesCloud != remote.Size)
                    {
                        // Remote Changed
                        if (snap.Status == SyncStatus.PendingUpload)
                        {
                            await LogAsync($"CONFLICTO detectado en: {relativePath}");
                            snap.Status = SyncStatus.Conflict;
                            await _audit.LogEventAsync("CONFLICT", "System", relativePath, "Both local and remote changed.");
                        }
                        else
                        {
                            await LogAsync($"Cambio remoto detectado: {relativePath}");
                            snap.Status = SyncStatus.PendingDownload;
                            snap.LastModifiedCloudUtc = remote.ModifiedTime;
                            snap.SizeBytesCloud = remote.Size;
                            snap.ETagCloud = remote.Md5Checksum;
                            await _audit.LogEventAsync("REMOTE_CHANGE_DETECTED", "System", relativePath, "File modified on Drive.");
                        }
                        await _store.UpsertSnapshotAsync(snap);
                    }
                }
                else
                {
                    // New Remote File
                    await LogAsync($"Nuevo archivo remoto: {relativePath}");
                    var newSnap = new FileSnapshot
                    {
                        RelativePath = relativePath,
                        DriveId = remote.Id,
                        DriveParentId = remote.ParentId,
                        SizeBytesCloud = remote.Size,
                        LastModifiedCloudUtc = remote.ModifiedTime,
                        ETagCloud = remote.Md5Checksum,
                        Status = SyncStatus.PendingDownload,
                        LastModifiedLocalUtc = DateTime.MinValue // Not present locally
                    };
                    await _store.UpsertSnapshotAsync(newSnap);
                    await _audit.LogEventAsync("NEW_REMOTE_FILE", "System", relativePath, "New file created on Drive.");
                }
            }
            
            // 5. Reconcile & Execute
            await ExecuteSyncActions(await _store.GetAllSnapshotsAsync());
            
            await LogAsync("Sincronización finalizada.");
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Sync Failed", ex);
            SyncStatusChanged?.Invoke("Error de sincronización.");
        }
    }

    private async Task ExecuteSyncActions(List<FileSnapshot> snapshots)
    {
        foreach (var snap in snapshots)
        {
            try
            {
                var localPath = Path.Combine(_localRootPath, snap.RelativePath);

                if (snap.Status == SyncStatus.PendingUpload)
                {
                    await LogAsync($"Subiendo: {snap.RelativePath}...");
                    // Use configured Drive Folder as default parent
                    var parentId = DriveFolderId!; 
                    
                    var newId = await _drive.UploadFileAsync(localPath, parentId, snap.DriveId);
                    snap.DriveId = newId;
                    snap.Status = SyncStatus.Synced;
                    snap.LastSyncAtUtc = DateTime.UtcNow;
                    snap.LastSyncDirection = "UP";
                    await _store.UpsertSnapshotAsync(snap);
                    await _audit.LogEventAsync("FILE_UPLOADED", "System", snap.RelativePath, "Upload successful.");
                }
                else if (snap.Status == SyncStatus.PendingDownload)
                {
                     await LogAsync($"Descargando: {snap.RelativePath}...");
                    if (snap.DriveId != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                        await _drive.DownloadFileAsync(snap.DriveId, localPath);
                        
                        var info = new FileInfo(localPath);
                        snap.SizeBytesLocal = info.Length;
                        snap.LastModifiedLocalUtc = info.LastWriteTimeUtc;
                        snap.Sha256Hash = await Hasher.CalculateSha256Async(localPath);
                        snap.Status = SyncStatus.Synced;
                        snap.LastSyncAtUtc = DateTime.UtcNow;
                        snap.LastSyncDirection = "DOWN";
                        await _store.UpsertSnapshotAsync(snap);
                        await _audit.LogEventAsync("FILE_DOWNLOADED", "System", snap.RelativePath, "Download successful.");
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Action failed for {snap.RelativePath}", ex);
            }
        }
    }

    private Dictionary<string, FileInfo> ScanLocalFiles()
    {
        var dir = new DirectoryInfo(_localRootPath);
        if (!dir.Exists) dir.Create();

        return dir.GetFiles("*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(_localRootPath, f.FullName), f => f);
    }

    private async Task LogAsync(string msg)
    {
        await _logger.LogAsync(msg);
        SyncStatusChanged?.Invoke(msg);
    }
}
