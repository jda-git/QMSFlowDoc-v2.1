using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

public enum SyncStatus
{
    Synced,
    PendingUpload,
    PendingDownload,
    Conflict,
    Error
}

public class FileSnapshot
{
    public string RelativePath { get; set; } = string.Empty;
    public string? DriveId { get; set; }
    public string? DriveParentId { get; set; }
    public long SizeBytesLocal { get; set; }
    public DateTime LastModifiedLocalUtc { get; set; }
    public long? SizeBytesCloud { get; set; }
    public DateTime? LastModifiedCloudUtc { get; set; }
    public string? ETagCloud { get; set; }
    public string? Sha256Hash { get; set; }
    public DateTime LastSyncAtUtc { get; set; }
    public string? LastSyncDirection { get; set; } // "UP" or "DOWN"
    public SyncStatus Status { get; set; }
    
    // Nuevos campos para migración a red
    public string? ConflictResolution { get; set; }  // "LOCAL_COPY_CREATED", "NETWORK_COPY_CREATED", "MANUAL"
    public DateTime? DeletedAt { get; set; }
    public string? TrashLocation { get; set; }  // Ruta relativa en _Trash/
    public string? FileGuid { get; set; }  // Para tracking de renames
}

public class SnapshotStore
{
    private readonly string _dbPath;

    public SnapshotStore()
    {
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "sync_snapshot.db");
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Snapshots (
                RelativePath TEXT PRIMARY KEY,
                DriveId TEXT,
                DriveParentId TEXT,
                SizeBytesLocal INTEGER,
                LastModifiedLocalUtc TEXT,
                SizeBytesCloud INTEGER,
                LastModifiedCloudUtc TEXT,
                ETagCloud TEXT,
                Sha256Hash TEXT,
                LastSyncAtUtc TEXT,
                LastSyncDirection TEXT,
                Status INTEGER,
                ConflictResolution TEXT,
                DeletedAt TEXT,
                TrashLocation TEXT,
                FileGuid TEXT
            );";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
        
        // Migración: agregar nuevas columnas si la tabla ya existe
        var alterSqls = new[]
        {
            "ALTER TABLE Snapshots ADD COLUMN ConflictResolution TEXT",
            "ALTER TABLE Snapshots ADD COLUMN DeletedAt TEXT",
            "ALTER TABLE Snapshots ADD COLUMN TrashLocation TEXT",
            "ALTER TABLE Snapshots ADD COLUMN FileGuid TEXT"
        };
        
        foreach (var alterSql in alterSqls)
        {
            try
            {
                using var alterCommand = new SqliteCommand(alterSql, connection);
                await alterCommand.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Columna ya existe, continuar
            }
        }
    }

    public async Task UpsertSnapshotAsync(FileSnapshot snapshot)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT OR REPLACE INTO Snapshots 
            (RelativePath, DriveId, DriveParentId, SizeBytesLocal, LastModifiedLocalUtc, SizeBytesCloud, LastModifiedCloudUtc, ETagCloud, Sha256Hash, LastSyncAtUtc, LastSyncDirection, Status, ConflictResolution, DeletedAt, TrashLocation, FileGuid)
            VALUES 
            ($path, $driveId, $parentId, $sizeLocal, $modLocal, $sizeCloud, $modCloud, $etag, $hash, $syncTime, $direction, $status, $conflictRes, $deletedAt, $trashLoc, $fileGuid);";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$path", snapshot.RelativePath);
        command.Parameters.AddWithValue("$driveId", snapshot.DriveId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$parentId", snapshot.DriveParentId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sizeLocal", snapshot.SizeBytesLocal);
        command.Parameters.AddWithValue("$modLocal", snapshot.LastModifiedLocalUtc.ToString("O"));
        command.Parameters.AddWithValue("$sizeCloud", snapshot.SizeBytesCloud ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$modCloud", snapshot.LastModifiedCloudUtc?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$etag", snapshot.ETagCloud ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$hash", snapshot.Sha256Hash ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$syncTime", snapshot.LastSyncAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$direction", snapshot.LastSyncDirection ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", (int)snapshot.Status);
        command.Parameters.AddWithValue("$conflictRes", snapshot.ConflictResolution ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$deletedAt", snapshot.DeletedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$trashLoc", snapshot.TrashLocation ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$fileGuid", snapshot.FileGuid ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<FileSnapshot?> GetSnapshotAsync(string relativePath)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Snapshots WHERE RelativePath = $path";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$path", relativePath);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadSnapshot(reader);
        }
        return null;
    }

    public async Task RemoveSnapshotAsync(string relativePath)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var command = new SqliteCommand("DELETE FROM Snapshots WHERE RelativePath = $path", connection);
        command.Parameters.AddWithValue("$path", relativePath);
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task<List<FileSnapshot>> GetAllSnapshotsAsync()
    {
        var list = new List<FileSnapshot>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = new SqliteCommand("SELECT * FROM Snapshots", connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSnapshot(reader));
        }
        return list;
    }
    
    // Nuevos métodos para network storage migration
    
    public async Task<List<FileSnapshot>> GetConflictsAsync()
    {
        var list = new List<FileSnapshot>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = new SqliteCommand("SELECT * FROM Snapshots WHERE Status = $status", connection);
        command.Parameters.AddWithValue("$status", (int)SyncStatus.Conflict);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSnapshot(reader));
        }
        return list;
    }
    
    public async Task<List<FileSnapshot>> GetPendingOperationsAsync()
    {
        var list = new List<FileSnapshot>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Snapshots WHERE Status IN ($up, $down)";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$up", (int)SyncStatus.PendingUpload);
        command.Parameters.AddWithValue("$down", (int)SyncStatus.PendingDownload);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSnapshot(reader));
        }
        return list;
    }
    
    public async Task<List<FileSnapshot>> GetDeletedFilesAsync()
    {
        var list = new List<FileSnapshot>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Snapshots WHERE DeletedAt IS NOT NULL";
        using var command = new SqliteCommand(sql, connection);
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSnapshot(reader));
        }
        return list;
    }
    
    // Helper privado para mapear reader a FileSnapshot
    private FileSnapshot ReadSnapshot(SqliteDataReader reader)
    {
        return new FileSnapshot
        {
            RelativePath = reader.GetString(0),
            DriveId = reader.IsDBNull(1) ? null : reader.GetString(1),
            DriveParentId = reader.IsDBNull(2) ? null : reader.GetString(2),
            SizeBytesLocal = reader.GetInt64(3),
            LastModifiedLocalUtc = DateTime.Parse(reader.GetString(4)),
            SizeBytesCloud = reader.IsDBNull(5) ? null : reader.GetInt64(5),
            LastModifiedCloudUtc = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
            ETagCloud = reader.IsDBNull(7) ? null : reader.GetString(7),
            Sha256Hash = reader.IsDBNull(8) ? null : reader.GetString(8),
            LastSyncAtUtc = DateTime.Parse(reader.GetString(9)),
            LastSyncDirection = reader.IsDBNull(10) ? null : reader.GetString(10),
            Status = (SyncStatus)reader.GetInt32(11),
            ConflictResolution = reader.IsDBNull(12) ? null : reader.GetString(12),
            DeletedAt = reader.IsDBNull(13) ? null : DateTime.Parse(reader.GetString(13)),
            TrashLocation = reader.IsDBNull(14) ? null : reader.GetString(14),
            FileGuid = reader.IsDBNull(15) ? null : reader.GetString(15)
        };
    }
}
