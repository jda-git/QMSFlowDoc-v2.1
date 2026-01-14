using Microsoft.Data.Sqlite;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.DTOs; // Added DTOs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

/// <summary>
/// Local document storage using SQLite (no PostgreSQL dependency)
/// </summary>
public class LocalDocumentStore
{
    private readonly string _dbPath;
    private readonly NetworkConfigStore _networkConfig;

    public LocalDocumentStore(NetworkConfigStore networkConfig)
    {
        _networkConfig = networkConfig;
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "documents_local.db");
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Folders (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ParentFolderId TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Documents (
                Id TEXT PRIMARY KEY,
                DocCode TEXT NOT NULL UNIQUE,
                Title TEXT NOT NULL,
                DocumentTypeId TEXT,
                FolderId TEXT,
                Area TEXT,
                Process TEXT,
                OwnerUserId TEXT,
                Status TEXT NOT NULL DEFAULT 'DRAFT',
                ReviewIntervalMonths INTEGER,
                NextReviewDue TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS DocumentVersions (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NOT NULL,
                VersionMajor INTEGER NOT NULL,
                VersionMinor INTEGER NOT NULL,
                VersionLabel TEXT NOT NULL,
                ChangeSummary TEXT,
                CreatedByUserId TEXT,
                CreatedAt TEXT NOT NULL,
                EffectiveFrom TEXT,
                LocalFilePath TEXT,
                FileName TEXT,
                MimeType TEXT,
                Sha256 TEXT,
                IsCurrent INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS DocumentTypes (
                Id TEXT PRIMARY KEY,
                TypeCode TEXT,
                Name TEXT NOT NULL,
                Description TEXT
            );

            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                UserId TEXT,
                UserName TEXT,
                Action TEXT NOT NULL,
                EntityType TEXT,
                EntityId TEXT,
                Details TEXT,
                MachineName TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_documents_code ON Documents(DocCode);
            CREATE INDEX IF NOT EXISTS idx_versions_document ON DocumentVersions(DocumentId);
            CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON AuditLogs(Timestamp);
        ";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();

        // Migration: Add columns to existing tables if missing
        try
        {
            using var colCmd = new SqliteCommand("ALTER TABLE Documents ADD COLUMN Area TEXT", connection);
            await colCmd.ExecuteNonQueryAsync();
        }
        catch { /* Column already exists */ }

        try
        {
            using var colCmd = new SqliteCommand("ALTER TABLE Documents ADD COLUMN Process TEXT", connection);
            await colCmd.ExecuteNonQueryAsync();
        }
        catch { /* Column already exists */ }

        // Seed Document Types
        await SeedDocumentTypesAsync(connection);
    }

    private async Task SeedDocumentTypesAsync(SqliteConnection connection)
    {
        var types = new List<(Guid Id, string Code, string Name)>
        {
            (Guid.Parse("362678f2-3e2b-4d40-b88a-268e364660e2"), "REP", "Reporte"),
            (Guid.Parse("7f1f3a2c-9a1d-4f4e-9b6f-78e7f8e7f8e7"), "INS", "Instructivo"),
            (Guid.Parse("8ca6789a-0b2c-4d3e-9f8a-7e6d5c4b3a21"), "FOR", "Formulario"),
            (Guid.Parse("b4c5d6e7-f8a9-4b0c-bd1d-2e3f4a5b6c7d"), "MAN", "Manual"),
            (Guid.Parse("e8f9a0b1-c2d3-4e4f-95a6-b7c8d9e0f1a2"), "EXT", "Externo"),
            (Guid.Parse("f1e2d3c4-b5a6-4078-9e0f-1a2b3c4d5e6f"), "PRO", "Procedimiento")
        };

        foreach (var t in types)
        {
            var checkSql = "SELECT COUNT(*) FROM DocumentTypes WHERE Id = $id";
            using var checkCmd = new SqliteCommand(checkSql, connection);
            checkCmd.Parameters.AddWithValue("$id", t.Id.ToString());
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                var insertSql = "INSERT INTO DocumentTypes (Id, TypeCode, Name) VALUES ($id, $code, $name)";
                using var insertCmd = new SqliteCommand(insertSql, connection);
                insertCmd.Parameters.AddWithValue("$id", t.Id.ToString());
                insertCmd.Parameters.AddWithValue("$code", t.Code);
                insertCmd.Parameters.AddWithValue("$name", t.Name);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<Document> CreateDocumentAsync(Document document)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO Documents (Id, DocCode, Title, DocumentTypeId, FolderId, Area, Process, OwnerUserId, Status, ReviewIntervalMonths, NextReviewDue, CreatedAt, UpdatedAt)
            VALUES ($id, $code, $title, $typeId, $folderId, $area, $process, $ownerId, $status, $interval, $nextReview, $created, $updated)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", document.Id.ToString());
        command.Parameters.AddWithValue("$code", document.DocCode);
        command.Parameters.AddWithValue("$title", document.Title);
        command.Parameters.AddWithValue("$typeId", document.DocumentTypeId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$folderId", document.FolderId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$area", document.Area ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$process", document.Process ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$ownerId", document.OwnerUserId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", document.Status.ToString());
        command.Parameters.AddWithValue("$interval", document.ReviewIntervalMonths ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$nextReview", document.NextReviewDue?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$created", document.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", document.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
        return document;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        try
        {
            // Get Folder Name for trash path
            var folderSql = "SELECT f.Name FROM Documents d LEFT JOIN Folders f ON d.FolderId = f.Id WHERE d.Id = $id";
            string folderName = "General";
            using (var cmd = new SqliteCommand(folderSql, connection))
            {
                cmd.Parameters.AddWithValue("$id", id.ToString());
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) folderName = result.ToString()!;
            }

            // Get file paths to delete physical files
            var versionsSql = "SELECT LocalFilePath FROM DocumentVersions WHERE DocumentId = $id";
            var filePaths = new List<string>();
            
            using (var cmd = new SqliteCommand(versionsSql, connection))
            {
                cmd.Parameters.AddWithValue("$id", id.ToString());
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        var path = reader.GetString(0);
                        if (!string.IsNullOrEmpty(path)) filePaths.Add(path);
                    }
                }
            }

            // Delete from database (cascade will delete versions)
            var deleteSql = "DELETE FROM Documents WHERE Id = $id";
            using (var cmd = new SqliteCommand(deleteSql, connection))
            {
                cmd.Parameters.AddWithValue("$id", id.ToString());
                var affected = await cmd.ExecuteNonQueryAsync();
                
                if (affected > 0)
                {
                    // Move files to Trash
                    var config = await _networkConfig.LoadAsync();
                    var datePrefix = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    
                    foreach (var filePath in filePaths)
                    {
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                var fileName = Path.GetFileName(filePath);
                                var trashLocalDir = Path.Combine(config.LocalBasePath, "_Trash", "local", datePrefix);
                                Directory.CreateDirectory(trashLocalDir);
                                
                                var newTrashPath = Path.Combine(trashLocalDir, $"{id}_{fileName}");
                                File.Move(filePath, newTrashPath, true);

                                // Try move network too if applicable
                                if (config.UseNetworkStorage)
                                {
                                    var oldNetworkPath = Path.Combine(config.NetworkBasePath, "Documentos", folderName, fileName);
                                    if (File.Exists(oldNetworkPath))
                                    {
                                        var trashNetworkDir = Path.Combine(config.NetworkBasePath, "_Trash", "network", datePrefix);
                                        Directory.CreateDirectory(trashNetworkDir);
                                        var newNetworkTrashPath = Path.Combine(trashNetworkDir, $"{id}_{fileName}");
                                        File.Move(oldNetworkPath, newNetworkTrashPath, true);
                                    }
                                }
                            }
                        }
                        catch 
                        { 
                            // Continue even if file move fails
                        }
                    }
                }
                return affected > 0;
            }
        }
        catch (Exception)
        {
            // Log error
            return false;
        }
    }

    public async Task<bool> UpdateDocumentAsync(Guid id, QMSFlowDoc.Shared.DTOs.CreateDocumentRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Get Current FolderId before update
        Guid? oldFolderId = null;
        var existingSql = "SELECT FolderId FROM Documents WHERE Id = $id";
        using (var checkCmd = new SqliteCommand(existingSql, connection))
        {
            checkCmd.Parameters.AddWithValue("$id", id.ToString());
            var result = await checkCmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value) oldFolderId = Guid.Parse(result.ToString()!);
        }

        var sql = @"
            UPDATE Documents 
            SET DocCode = $code, 
                Title = $title, 
                DocumentTypeId = $typeId, 
                FolderId = $folderId, 
                Area = $area, 
                Process = $process, 
                Status = $status,
                ReviewIntervalMonths = $interval,
                UpdatedAt = $updated
            WHERE Id = $id
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$code", request.DocCode);
        command.Parameters.AddWithValue("$title", request.Title);
        command.Parameters.AddWithValue("$typeId", request.DocumentTypeId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$folderId", request.FolderId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$area", request.Area ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$process", request.Process ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", request.Status?.ToString() ?? DocumentStatus.DRAFT.ToString());
        command.Parameters.AddWithValue("$interval", request.ReviewIntervalMonths);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));

        var affected = await command.ExecuteNonQueryAsync();

        if (affected > 0)
        {
            // Detect Folder Change
            if (oldFolderId != request.FolderId)
            {
                await HandleFolderMoveAsync(id, oldFolderId, request.FolderId);
            }

            // Also update Version Label if provided
            if (!string.IsNullOrWhiteSpace(request.VersionLabel))
            {
                var versionSql = "UPDATE DocumentVersions SET VersionLabel = $label WHERE DocumentId = $id AND IsCurrent = 1";
                using var versionCmd = new SqliteCommand(versionSql, connection);
                versionCmd.Parameters.AddWithValue("$label", request.VersionLabel);
                versionCmd.Parameters.AddWithValue("$id", id.ToString());
                await versionCmd.ExecuteNonQueryAsync();
            }
        }

        return affected > 0;
    }

    private async Task HandleFolderMoveAsync(Guid docId, Guid? oldFolderId, Guid? newFolderId)
    {
        var config = await _networkConfig.LoadAsync();
        var oldFolderName = oldFolderId.HasValue ? await GetFolderNameByIdAsync(oldFolderId.Value) : "General";
        var newFolderName = newFolderId.HasValue ? await GetFolderNameByIdAsync(newFolderId.Value) : "General";

        if (string.IsNullOrEmpty(oldFolderName)) oldFolderName = "General";
        if (string.IsNullOrEmpty(newFolderName)) newFolderName = "General";

        var versions = await GetVersionsAsync(docId);
        foreach (var version in versions)
        {
            if (string.IsNullOrEmpty(version.LocalFilePath)) continue;

            try
            {
                // 1. Local Move
                var fileName = Path.GetFileName(version.LocalFilePath);
                var newLocalPath = Path.Combine(config.LocalBasePath, "Documentos", newFolderName, fileName);
                
                if (File.Exists(version.LocalFilePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newLocalPath)!);
                    File.Move(version.LocalFilePath, newLocalPath, true);
                }

                // 2. Network Move
                if (config.UseNetworkStorage)
                {
                    var oldNetworkPath = Path.Combine(config.NetworkBasePath, "Documentos", oldFolderName, fileName);
                    var newNetworkPath = Path.Combine(config.NetworkBasePath, "Documentos", newFolderName, fileName);
                    
                    if (File.Exists(oldNetworkPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newNetworkPath)!);
                        File.Move(oldNetworkPath, newNetworkPath, true);
                    }
                }

                // 3. Update DB Path
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();
                var updateSql = "UPDATE DocumentVersions SET LocalFilePath = $path WHERE Id = $id";
                using var cmd = new SqliteCommand(updateSql, connection);
                cmd.Parameters.AddWithValue("$path", newLocalPath);
                cmd.Parameters.AddWithValue("$id", version.Id.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Log or handle error? For now continue
                System.Diagnostics.Debug.WriteLine($"Error moving file for version {version.Id}: {ex.Message}");
            }
        }
    }

    public async Task<Document?> GetDocumentByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Documents WHERE Id = $id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var doc = MapDocument(reader);
            doc.Versions = await GetVersionsAsync(doc.Id);
            return doc;
        }
        return null;
    }

    public async Task<List<Document>> GetAllDocumentsAsync(bool includeObsolete = false)
    {
        var documents = new List<Document>();
        
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Documents";
        if (!includeObsolete)
        {
            sql += " WHERE Status <> 'OBSOLETE'";
        }
        sql += " ORDER BY UpdatedAt DESC";

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var doc = MapDocument(reader);
            doc.Versions = await GetVersionsAsync(doc.Id); // Load versions for each doc
            documents.Add(doc);
        }

        return documents;
    }

    public async Task LogAuditAsync(AuditLog log)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO AuditLogs (Id, Timestamp, UserId, UserName, Action, EntityType, EntityId, Details, MachineName)
            VALUES ($id, $ts, $userId, $userName, $action, $entityType, $entityId, $details, $machine)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", log.Id.ToString());
        command.Parameters.AddWithValue("$ts", log.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$userId", log.UserId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$userName", log.UserName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$action", log.Action);
        command.Parameters.AddWithValue("$entityType", log.EntityType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$entityId", log.EntityId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$details", log.Details ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$machine", log.MachineName ?? Environment.MachineName);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DocumentVersion> AddVersionAsync(DocumentVersion version)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO DocumentVersions 
            (Id, DocumentId, VersionMajor, VersionMinor, VersionLabel, ChangeSummary, CreatedByUserId, CreatedAt, EffectiveFrom, LocalFilePath, FileName, MimeType, Sha256, IsCurrent)
            VALUES ($id, $docId, $major, $minor, $label, $summary, $userId, $created, $effective, $path, $fileName, $mime, $sha256, $current)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", version.Id.ToString());
        command.Parameters.AddWithValue("$docId", version.DocumentId.ToString());
        command.Parameters.AddWithValue("$major", version.VersionMajor);
        command.Parameters.AddWithValue("$minor", version.VersionMinor);
        command.Parameters.AddWithValue("$label", version.VersionLabel);
        command.Parameters.AddWithValue("$summary", version.ChangeSummary ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$userId", version.CreatedByUserId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$created", version.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$effective", version.EffectiveFrom?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$path", version.LocalFilePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$fileName", version.FileName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$mime", version.MimeType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sha256", version.Sha256 ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$current", version.IsCurrent ? 1 : 0);

        await command.ExecuteNonQueryAsync();
        return version;
    }

    /// <summary>
    /// Create document with file - copies to local/network folders automatically
    /// </summary>
    public async Task<(Document Document, DocumentVersion Version)> CreateDocumentWithFileAsync(
        string docCode,
        string title,
        DocumentStatus status,
        Guid? documentTypeId,
        int? reviewIntervalMonths,
        string versionLabel,
        string? area,                   // Added
        string? process,                // Added
        byte[] fileBytes,
        string fileName,
        string subFolderName)
    {
        // 0. PDF Restriction
        if (!Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se permiten archivos .PDF en el gestor documental.");
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // 1. Resolve FolderId
        Guid? folderId = null;
        if (!string.IsNullOrWhiteSpace(subFolderName))
        {
            folderId = await FindOrCreateFolderIdAsync(subFolderName);
        }

        // Calculate Next Review
        DateTime? nextReview = null;
        if (reviewIntervalMonths.HasValue && reviewIntervalMonths.Value > 0)
        {
            nextReview = DateTime.UtcNow.AddMonths(reviewIntervalMonths.Value);
        }

        // 2. Create document
        var document = new Document
        {
            Id = Guid.NewGuid(),
            DocCode = docCode,
            Title = title,
            Status = status,
            FolderId = folderId,
            DocumentTypeId = documentTypeId,     // Set Type
            Area = area,                         // Set Area
            Process = process,                   // Set Process
            ReviewIntervalMonths = reviewIntervalMonths ?? 12, // Set Interval
            NextReviewDue = nextReview,          // Set Next Review
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Check if document already exists
        var existingDoc = await GetDocumentByIdAsync(document.Id);
        if (existingDoc == null)
        {
            // Try find by code if ID is new (e.g. from UI that doesn't know ID yet)
            var sqlFind = "SELECT Id FROM Documents WHERE DocCode = $code";
            using (var cmdFind = new SqliteCommand(sqlFind, connection))
            {
                cmdFind.Parameters.AddWithValue("$code", docCode);
                var result = await cmdFind.ExecuteScalarAsync();
                if (result != null) existingDoc = await GetDocumentByIdAsync(Guid.Parse(result.ToString()!));
            }
        }

        if (existingDoc != null)
        {
            document.Id = existingDoc.Id; // Sync IDs
            await ArchiveOldVersionsAsync(existingDoc.Id, connection);
            
            // Update metadata
            var sqlUpdate = "UPDATE Documents SET Title=$title, DocumentTypeId=$typeId, FolderId=$folderId, Area=$area, Process=$process, UpdatedAt=$updated WHERE Id=$id";
            using var cmdUpdate = new SqliteCommand(sqlUpdate, connection);
            cmdUpdate.Parameters.AddWithValue("$id", document.Id.ToString());
            cmdUpdate.Parameters.AddWithValue("$title", title);
            cmdUpdate.Parameters.AddWithValue("$typeId", documentTypeId?.ToString() ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$folderId", folderId?.ToString() ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$area", area ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$process", process ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
            await cmdUpdate.ExecuteNonQueryAsync();
        }
        else
        {
            await CreateDocumentAsync(document);
        }

        // 2. Determine file paths
        var config = await _networkConfig.LoadAsync();
        string localPath, networkPath;

        if (config != null && config.UseNetworkStorage)
        {
            // Use configured paths with subfolder
            // config.LocalBasePath is typically: C:\...\QMS
            // We want: QMS\Documentos\[category]\[file]
            var category = !string.IsNullOrWhiteSpace(subFolderName) ? subFolderName : "General";
            var fileNameSafe = $"{docCode}_v1.0{Path.GetExtension(fileName)}";

            localPath = Path.Combine(config.LocalBasePath, "Documentos", category, fileNameSafe);
            networkPath = Path.Combine(config.NetworkBasePath, "Documentos", category, fileNameSafe);
        }
        else
        {
            // Fallback to default location
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var defaultFolder = Path.Combine(localAppData, "QMSFlowDoc", "Files");
            Directory.CreateDirectory(defaultFolder);
            localPath = Path.Combine(defaultFolder, $"{docCode}_v1.0{Path.GetExtension(fileName)}");
            networkPath = localPath; // Same as local if no network
        }

        // 3. Copy file to local folder
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await File.WriteAllBytesAsync(localPath, fileBytes);

        // 4. Copy to network if different
        if (localPath != networkPath && config?.UseNetworkStorage == true)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(networkPath)!);
                await File.WriteAllBytesAsync(networkPath, fileBytes);
            }
            catch
            {
                // Network copy failed - will sync later
            }
        }

        // 5. Create version record
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionMajor = 1, 
            VersionMinor = 0,
            VersionLabel = !string.IsNullOrWhiteSpace(versionLabel) ? versionLabel : "1.0", // Use user version
            ChangeSummary = "Initial version via Local Mode",
            CreatedAt = DateTime.UtcNow,
            FileName = fileName,
            LocalFilePath = localPath,
            MimeType = "application/pdf",
            IsCurrent = true
        };

        await AddVersionAsync(version);

        return (document, version);
    }

    private async Task ArchiveOldVersionsAsync(Guid documentId, SqliteConnection connection)
    {
        var versions = await GetVersionsAsync(documentId);
        var config = await _networkConfig.LoadAsync();
        var dateStr = DateTime.Now.ToString("dd-MM-yy");

        foreach (var v in versions)
        {
            if (v.IsCurrent && !string.IsNullOrEmpty(v.LocalFilePath) && File.Exists(v.LocalFilePath))
            {
                try
                {
                    var oldFileName = v.FileName ?? Path.GetFileName(v.LocalFilePath);
                    var archivedName = $"RETIRADO_{dateStr}_{oldFileName}";
                    var archiveLocalDir = Path.Combine(config.LocalBasePath, "Documentos", "VERSIONES ANTIGUAS");
                    Directory.CreateDirectory(archiveLocalDir);

                    var newPath = Path.Combine(archiveLocalDir, archivedName);
                    File.Move(v.LocalFilePath, newPath, true);

                    // Update DB record
                    var sql = "UPDATE DocumentVersions SET LocalFilePath = $path, IsCurrent = 0 WHERE Id = $id";
                    using var cmd = new SqliteCommand(sql, connection);
                    cmd.Parameters.AddWithValue("$path", newPath);
                    cmd.Parameters.AddWithValue("$id", v.Id.ToString());
                    await cmd.ExecuteNonQueryAsync();

                    // Optional: Try archive network too
                    if (config.UseNetworkStorage && !string.IsNullOrEmpty(config.NetworkBasePath))
                    {
                         try
                         {
                             // Calculate the relative path from local base to the old file
                             var relPath = Path.GetRelativePath(config.LocalBasePath, v.LocalFilePath);
                             var oldNetworkPath = Path.Combine(config.NetworkBasePath, relPath);

                             if (File.Exists(oldNetworkPath))
                             {
                                 var archiveNetworkDir = Path.Combine(config.NetworkBasePath, "Documentos", "VERSIONES ANTIGUAS");
                                 Directory.CreateDirectory(archiveNetworkDir);
                                 var newNetworkPath = Path.Combine(archiveNetworkDir, archivedName);
                                 File.Move(oldNetworkPath, newNetworkPath, true);
                             }
                         }
                         catch (Exception netEx)
                         {
                             System.Diagnostics.Debug.WriteLine($"Network archival error: {netEx.Message}");
                         }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Archival error: {ex.Message}");
                }
            }
            else if (v.IsCurrent)
            {
                // Mark as not current even if file missing
                var sql = "UPDATE DocumentVersions SET IsCurrent = 0 WHERE Id = $id";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("$id", v.Id.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task<Guid> FindOrCreateFolderIdAsync(string folderName)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Try find existing
        var selectSql = "SELECT Id FROM Folders WHERE Name = $name COLLATE NOCASE LIMIT 1";
        using (var cmd = new SqliteCommand(selectSql, connection))
        {
            cmd.Parameters.AddWithValue("$name", folderName);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return Guid.Parse(result.ToString()!);
            }
        }

        // Create new
        var newId = Guid.NewGuid();
        var insertSql = "INSERT INTO Folders (Id, Name, CreatedAt) VALUES ($id, $name, $created)";
        using (var cmd = new SqliteCommand(insertSql, connection))
        {
            cmd.Parameters.AddWithValue("$id", newId.ToString());
            cmd.Parameters.AddWithValue("$name", folderName);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        return newId;
    }

    private Document MapDocument(SqliteDataReader reader)
    {
        return new Document
        {
            Id = Guid.Parse(reader.GetString(0)),
            DocCode = reader.GetString(1),
            Title = reader.GetString(2),
            DocumentTypeId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
            FolderId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
            Area = reader.IsDBNull(5) ? null : reader.GetString(5),
            Process = reader.IsDBNull(6) ? null : reader.GetString(6),
            OwnerUserId = reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)),
            Status = Enum.Parse<DocumentStatus>(reader.GetString(8)),
            ReviewIntervalMonths = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            NextReviewDue = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10)),
            CreatedAt = DateTime.Parse(reader.GetString(11)),
            UpdatedAt = DateTime.Parse(reader.GetString(12))
        };
    }

    public async Task<List<DocumentType>> GetDocumentTypesAsync()
    {
        var types = new List<DocumentType>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM DocumentTypes ORDER BY Name";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            types.Add(new DocumentType
            {
                Id = Guid.Parse(reader.GetString(0)),
                TypeCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        return types;
    }

    private async Task<List<DocumentVersion>> GetVersionsAsync(Guid docId)
    {
        var versions = new List<DocumentVersion>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM DocumentVersions WHERE DocumentId = $docId ORDER BY CreatedAt DESC";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$docId", docId.ToString());

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            versions.Add(new DocumentVersion
            {
                Id = Guid.Parse(reader.GetString(0)),
                DocumentId = Guid.Parse(reader.GetString(1)),
                VersionMajor = reader.GetInt32(2),
                VersionMinor = reader.GetInt32(3),
                VersionLabel = reader.GetString(4),
                ChangeSummary = reader.IsDBNull(5) ? "" : reader.GetString(5),
                CreatedByUserId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                EffectiveFrom = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                LocalFilePath = reader.IsDBNull(9) ? "" : reader.GetString(9),
                FileName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                MimeType = reader.IsDBNull(11) ? "" : reader.GetString(11),
                Sha256 = reader.IsDBNull(12) ? "" : reader.GetString(12),
                IsCurrent = reader.GetInt32(13) == 1
            });
        }
        return versions;
    }
    public async Task<List<FolderDto>> GetFoldersAsync()
    {
        var folders = new List<FolderDto>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            SELECT f.Id, f.Name, f.ParentFolderId, 
            (SELECT COUNT(*) FROM Folders sub WHERE sub.ParentFolderId = f.Id) as SubCount,
            (SELECT COUNT(*) FROM Documents d WHERE d.FolderId = f.Id) as DocCount
            FROM Folders f
            ORDER BY f.Name";
            
        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            folders.Add(new FolderDto(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
                reader.GetInt32(3),
                reader.GetInt32(4)
            ));
        }
        return folders;
    }

    public async Task<bool> CreateFolderAsync(string name, Guid? parentId)
    {
        // 1. Check if folder already exists (read-only check)
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        // Find existing ID
        var checkSql = "SELECT Id FROM Folders WHERE Name = $name COLLATE NOCASE LIMIT 1";
        Guid? existingId = null;
        using (var cmd = new SqliteCommand(checkSql, connection))
        {
            cmd.Parameters.AddWithValue("$name", name);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                existingId = Guid.Parse(result.ToString()!);
            }
        }

        if (existingId.HasValue)
        {
             // Already exists. Return true as "success" (idempotent) or false if we want to signal "already existed"
             // For UI "Create Folder", usually we don't want to create duplicates. 
             // Returning true implies "folder is there now".
             return true; 
        }

        // 2. Create new
        var id = Guid.NewGuid();
        var insertSql = "INSERT INTO Folders (Id, Name, ParentFolderId, CreatedAt) VALUES ($id, $name, $parent, $created)";
        
        using (var cmd = new SqliteCommand(insertSql, connection))
        {
            cmd.Parameters.AddWithValue("$id", id.ToString());
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$parent", parentId?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }

    public async Task<bool> RenameFolderAsync(Guid id, string newName)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "UPDATE Folders SET Name = $name WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$name", newName);
        
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteFolderAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "DELETE FROM Folders WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
    // Document Types
    public async Task<DocumentType?> GetDocumentTypeByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM DocumentTypes WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new DocumentType
            {
                Id = Guid.Parse(reader.GetString(0)),
                TypeCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            };
        }
        return null;
    }

    public async Task<string?> GetFolderNameByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var sql = "SELECT Name FROM Folders WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }
}
