using System;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

/// <summary>
/// Resuelve conflictos de sincronización creando copias timestamped
/// </summary>
public class ConflictResolver
{
    private readonly SnapshotStore _snapshotStore;
    private readonly IAuditLogger _auditLogger;

    public ConflictResolver(SnapshotStore snapshotStore, IAuditLogger auditLogger)
    {
        _snapshotStore = snapshotStore;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Resuelve un conflicto creando copias en ambos lados
    /// </summary>
    /// <param name="relativePath">Ruta relativa del archivo en conflicto</param>
    /// <param name="localFullPath">Ruta completa local</param>
    /// <param name="networkFullPath">Ruta completa en red</param>
    /// <param name="localModifiedUtc">Fecha de modificación local</param>
    /// <param name="networkModifiedUtc">Fecha de modificación en red</param>
    public async Task<ConflictResolution> ResolveConflictAsync(
        string relativePath,
        string localFullPath,
        string networkFullPath,
        DateTime localModifiedUtc,
        DateTime networkModifiedUtc)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmm");
        var extension = Path.GetExtension(relativePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
        var directory = Path.GetDirectoryName(relativePath) ?? "";

        // Crear nombre de archivo de conflicto local
        var localConflictFileName = $"{fileNameWithoutExt} (CONFLICT local {timestamp}){extension}";
        var localConflictPath = Path.Combine(Path.GetDirectoryName(localFullPath) ?? "", localConflictFileName);
        var localConflictRelativePath = Path.Combine(directory, localConflictFileName);

        // Crear nombre de archivo de conflicto red
        var networkConflictFileName = $"{fileNameWithoutExt} (CONFLICT red {timestamp}){extension}";
        var networkConflictPath = Path.Combine(Path.GetDirectoryName(networkFullPath) ?? "", networkConflictFileName);
        var networkConflictRelativePath = Path.Combine(directory, networkConflictFileName);

        try
        {
            // Copiar versión local con sufijo de conflicto
            if (File.Exists(localFullPath))
            {
                File.Copy(localFullPath, localConflictPath, overwrite: false);
            }

            // Copiar versión de red con sufijo de conflicto
            if (File.Exists(networkFullPath))
            {
                File.Copy(networkFullPath, networkConflictPath, overwrite: false);
            }

            // Actualizar snapshot con información del conflicto
            var snapshot = await _snapshotStore.GetSnapshotAsync(relativePath);
            if (snapshot != null)
            {
                snapshot.Status = SyncStatus.Conflict;
                snapshot.ConflictResolution = $"LOCAL_COPY:{localConflictRelativePath}|NETWORK_COPY:{networkConflictRelativePath}";
                await _snapshotStore.UpsertSnapshotAsync(snapshot);
            }

            // Registrar en audit trail
            await _auditLogger.LogEventAsync(
                "CONFLICT_DETECTED",
                "System",
                relativePath,
                $"Local: {localModifiedUtc:yyyy-MM-dd HH:mm:ss} | Network: {networkModifiedUtc:yyyy-MM-dd HH:mm:ss}");

            return new ConflictResolution
            {
                Success = true,
                LocalConflictPath = localConflictRelativePath,
                NetworkConflictPath = networkConflictRelativePath,
                Message = $"Conflicto resuelto: copias creadas con sufijos timestamped"
            };
        }
        catch (Exception ex)
        {
            await _auditLogger.LogEventAsync(
                "CONFLICT_RESOLUTION_FAILED",
                "System",
                relativePath,
                $"Error: {ex.Message}");

            return new ConflictResolution
            {
                Success = false,
                Message = $"Error al resolver conflicto: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Determina si hay un conflicto basado en timestamps y hashes
    /// </summary>
    public bool IsConflict(
        FileSnapshot? snapshot,
        DateTime localModified,
        DateTime networkModified,
        string? localHash,
        string? networkHash)
    {
        // Si no hay snapshot, no hay conflicto (archivo nuevo)
        if (snapshot == null)
            return false;

        // Si los hashes son iguales, no hay conflicto (contenido idéntico)
        if (!string.IsNullOrEmpty(localHash) && 
            !string.IsNullOrEmpty(networkHash) && 
            localHash == networkHash)
            return false;

        // Si ambos han cambiado desde la última sincronización
        var localChanged = snapshot.LastSyncAtUtc < localModified;
        var networkChanged = snapshot.LastSyncAtUtc < networkModified;

        return localChanged && networkChanged;
    }

    /// <summary>
    /// Genera un nombre de archivo de conflicto con timestamp
    /// </summary>
    public static string GenerateConflictFileName(string originalPath, string conflictType)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmm");
        var extension = Path.GetExtension(originalPath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
        var directory = Path.GetDirectoryName(originalPath);

        var conflictFileName = $"{fileNameWithoutExt} (CONFLICT {conflictType} {timestamp}){extension}";
        
        return string.IsNullOrEmpty(directory) 
            ? conflictFileName 
            : Path.Combine(directory, conflictFileName);
    }
}

/// <summary>
/// Resultado de la resolución de conflicto
/// </summary>
public class ConflictResolution
{
    public bool Success { get; set; }
    public string? LocalConflictPath { get; set; }
    public string? NetworkConflictPath { get; set; }
    public string Message { get; set; } = "";
}
