using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services.Storage;

namespace QMSFlowDoc.Client.Services.Sync;

/// <summary>
/// Represents a pending change to be synchronized
/// </summary>
public class SyncChange
{
    public string RelativePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);
    public string Folder => Path.GetDirectoryName(RelativePath) ?? "";
    public DateTime? LocalModified { get; set; }
    public DateTime? NetworkModified { get; set; }
    public long LocalSize { get; set; }
    public long NetworkSize { get; set; }
    public SyncDirection Direction { get; set; }
    public bool IsConflict { get; set; }
    public SyncDirection ConflictResolution { get; set; } = SyncDirection.None;
    public bool IsNewerButSmaller { get; set; } // Flag for potential data loss

    public string LocalSizeFormatted => FormatSize(LocalSize);
    public string NetworkSizeFormatted => FormatSize(NetworkSize);
    
    public string WarningMessage => IsNewerButSmaller 
        ? "⚠️ ¡PELIGRO! Versión local VACÍA o MENOR. Se recomienda 'Usar Red'." 
        : "";

    public string DirectionText => Direction switch
    {
        SyncDirection.Download => "📥 Descargar (Red → Local)",
        SyncDirection.Upload => "📤 Subir (Local → Red)",
        SyncDirection.Conflict => "⚠️ Conflicto",
        _ => "Sin cambios"
    };

    private string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1024 / 1024.0:F1} MB";
    }
}

public enum SyncDirection
{
    None,
    Download,
    Upload,
    Conflict
}

/// <summary>
/// Service for bidirectional sync between Local Workspace and Network Repository
/// </summary>
public class NetworkSyncService
{
    private readonly NetworkConfigStore _configStore;
    private readonly ISyncLogger _logger;
    private const int TimeTolerance = 5; // seconds

    public event Action<string>? SyncStatusChanged;

    // Folders to sync (relative to base path)
    private static readonly string[] SyncFolders = new[]
    {
        "Auditoria",
        "Controles_Externos",
        "Documentos",
        "Equipos",
        "Incidencias",
        "Inventario",
        "Metodos",
        "Personal",
        "Base_datos",
        "Informes_Generados"
    };

    public NetworkSyncService(NetworkConfigStore configStore, ISyncLogger logger)
    {
        _configStore = configStore;
        _logger = logger;
    }

    // Legacy constructor for compatibility
    public NetworkSyncService(NetworkConfigStore configStore, string localDbPath, string localFilesPath)
    {
        _configStore = configStore;
        _logger = new SyncLogger();
    }

    /// <summary>
    /// Gets list of pending changes without executing sync
    /// </summary>
    public async Task<List<SyncChange>> GetPendingChangesAsync()
    {
        var changes = new List<SyncChange>();
        
        try
        {
            var config = await _configStore.LoadAsync();
            if (string.IsNullOrEmpty(config.NetworkBasePath) || string.IsNullOrEmpty(config.LocalBasePath))
            {
                return changes;
            }

            var networkProvider = new NetworkStorageProvider(config.NetworkBasePath);
            if (!await networkProvider.TestConnectionAsync())
            {
                await _logger.LogAsync("GetPendingChanges: No se pudo conectar a la red", "WARN");
                return changes;
            }

            // Scan all sync folders
            foreach (var folder in SyncFolders)
            {
                var folderChanges = await ScanFolderForChangesAsync(
                    config.LocalBasePath, 
                    config.NetworkBasePath, 
                    folder);
                changes.AddRange(folderChanges);
            }

            await _logger.LogAsync($"GetPendingChanges: {changes.Count} cambios detectados");
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Error getting pending changes", ex);
        }

        return changes;
    }

    private async Task<List<SyncChange>> ScanFolderForChangesAsync(string localBase, string networkBase, string relativeFolder)
    {
        var changes = new List<SyncChange>();
        
        var localPath = Path.Combine(localBase, relativeFolder);
        var networkPath = Path.Combine(networkBase, relativeFolder);

        // Ensure directories exist
        Directory.CreateDirectory(localPath);
        Directory.CreateDirectory(networkPath);

        // Get all files from both locations
        var localFiles = GetFilesRecursive(localPath, localBase);
        var networkFiles = GetFilesRecursive(networkPath, networkBase);

        var allPaths = localFiles.Keys.Union(networkFiles.Keys).Distinct();

        foreach (var relativePath in allPaths)
        {
            var hasLocal = localFiles.TryGetValue(relativePath, out var localInfo);
            var hasNetwork = networkFiles.TryGetValue(relativePath, out var networkInfo);

            if (hasLocal && hasNetwork)
            {
                // Both exist - check for differences
                var localMod = localInfo!.LastWriteTimeUtc;
                var networkMod = networkInfo!.LastWriteTimeUtc;

                if (Math.Abs((localMod - networkMod).TotalSeconds) > TimeTolerance)
                {
                    // Files differ - determine direction based on Last Write Wins
                    if (localMod > networkMod.AddSeconds(TimeTolerance) && networkMod > localMod.AddSeconds(-TimeTolerance * 10))
                    {
                        // Both modified recently (potential conflict)
                        changes.Add(new SyncChange
                        {
                            RelativePath = relativePath,
                            LocalModified = localMod,
                            NetworkModified = networkMod,
                            LocalSize = localInfo.Length,
                            NetworkSize = networkInfo.Length,
                            Direction = SyncDirection.Conflict,
                            IsConflict = true
                        });
                    }
                    else if (localMod > networkMod)
                    {
                        // Local is newer. Check for "Newer but Smaller" (potential fresh overwrite)
                        if (localInfo.Length < networkInfo.Length && networkInfo.Length > 0)
                        {
                            changes.Add(new SyncChange
                            {
                                RelativePath = relativePath,
                                LocalModified = localMod,
                                NetworkModified = networkMod,
                                LocalSize = localInfo.Length,
                                NetworkSize = networkInfo.Length,
                                Direction = SyncDirection.Conflict, // Force user decision
                                IsConflict = true,
                                IsNewerButSmaller = true,
                                ConflictResolution = SyncDirection.Download // Default to keeping Network (Master)
                            });
                        }
                        else
                        {
                            changes.Add(new SyncChange
                            {
                                RelativePath = relativePath,
                                LocalModified = localMod,
                                NetworkModified = networkMod,
                                LocalSize = localInfo.Length,
                                NetworkSize = networkInfo.Length,
                                Direction = SyncDirection.Upload
                            });
                        }
                    }
                    else
                    {
                        changes.Add(new SyncChange
                        {
                            RelativePath = relativePath,
                            LocalModified = localMod,
                            NetworkModified = networkMod,
                            LocalSize = localInfo.Length,
                            NetworkSize = networkInfo.Length,
                            Direction = SyncDirection.Download
                        });
                    }
                }
            }
            else if (hasLocal && !hasNetwork)
            {
                // Only local exists - upload
                changes.Add(new SyncChange
                {
                    RelativePath = relativePath,
                    LocalModified = localInfo!.LastWriteTimeUtc,
                    NetworkModified = null,
                    LocalSize = localInfo.Length,
                    Direction = SyncDirection.Upload
                });
            }
            else if (!hasLocal && hasNetwork)
            {
                // Only network exists - download
                changes.Add(new SyncChange
                {
                    RelativePath = relativePath,
                    LocalModified = null,
                    NetworkModified = networkInfo!.LastWriteTimeUtc,
                    NetworkSize = networkInfo.Length,
                    Direction = SyncDirection.Download
                });
            }
        }

        return await Task.FromResult(changes);
    }

    private Dictionary<string, FileInfo> GetFilesRecursive(string basePath, string rootPath)
    {
        var result = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        
        if (!Directory.Exists(basePath)) return result;

        try
        {
            var files = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\_Trash\\") && !f.Contains("\\._") && !Path.GetFileName(f).StartsWith("."));

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(rootPath, file);
                result[relativePath] = new FileInfo(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip folders without access
        }

        return result;
    }

    /// <summary>
    /// Executes sync for all pending changes
    /// </summary>
    public async Task SyncAllAsync()
    {
        var changes = await GetPendingChangesAsync();
        await ExecuteSyncAsync(changes);
    }

    /// <summary>
    /// Executes sync for specific changes (after user confirmation)
    /// </summary>
    public async Task ExecuteSyncAsync(List<SyncChange> changes)
    {
        if (!changes.Any())
        {
            SyncStatusChanged?.Invoke("Sin cambios pendientes.");
            return;
        }

        try
        {
            var config = await _configStore.LoadAsync();
            var networkProvider = new NetworkStorageProvider(config.NetworkBasePath);

            if (!await networkProvider.TestConnectionAsync())
            {
                SyncStatusChanged?.Invoke("Error: No se puede acceder a la red.");
                return;
            }

            int uploaded = 0, downloaded = 0, conflicts = 0;

            foreach (var change in changes)
            {
                try
                {
                    var localPath = Path.Combine(config.LocalBasePath, change.RelativePath);
                    var networkPath = Path.Combine(config.NetworkBasePath, change.RelativePath);

                    var direction = change.IsConflict ? change.ConflictResolution : change.Direction;

                    if (direction == SyncDirection.Upload)
                    {
                        SyncStatusChanged?.Invoke($"Subiendo: {change.FileName}...");
                        Directory.CreateDirectory(Path.GetDirectoryName(networkPath)!);
                        File.Copy(localPath, networkPath, overwrite: true);
                        uploaded++;
                        await _logger.LogAsync($"UPLOADED: {change.RelativePath}");
                    }
                    else if (direction == SyncDirection.Download)
                    {
                        SyncStatusChanged?.Invoke($"Descargando: {change.FileName}...");
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                        File.Copy(networkPath, localPath, overwrite: true);
                        downloaded++;
                        await _logger.LogAsync($"DOWNLOADED: {change.RelativePath}");
                    }
                    else if (change.IsConflict && direction == SyncDirection.None)
                    {
                        conflicts++;
                        await _logger.LogAsync($"CONFLICT SKIPPED: {change.RelativePath}", "WARN");
                    }
                }
                catch (Exception ex)
                {
                    await _logger.LogErrorAsync($"Error syncing {change.RelativePath}", ex);
                }
            }

            SyncStatusChanged?.Invoke($"Sync completado: {uploaded} subidos, {downloaded} descargados, {conflicts} conflictos.");
            
            // Update last sync time
            config.LastSyncAt = DateTime.UtcNow;
            await _configStore.SaveAsync(config);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Fatal sync error", ex);
            SyncStatusChanged?.Invoke($"Error fatal: {ex.Message}");
        }
    }

    /// <summary>
    /// Quick check if there are any pending changes
    /// </summary>
    public async Task<bool> HasPendingChangesAsync()
    {
        var changes = await GetPendingChangesAsync();
        return changes.Any();
    }
}
