using Microsoft.Data.Sqlite;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface ILocalCacheService
{
    Task InitializeAsync();
    Task CacheDocumentsAsync(IEnumerable<DocumentDto> documents);
    Task<IEnumerable<DocumentDto>> GetCachedDocumentsAsync();
    Task SaveDocumentContentAsync(Guid documentId, Stream content, string fileName);
    Task<string?> GetCachedFilePathAsync(Guid documentId);
}

public class LocalCacheService : ILocalCacheService
{
    private readonly string _dbPath;
    private readonly string _filesPath;

    public LocalCacheService()
    {
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc");
        Directory.CreateDirectory(appFolder);
        
        _dbPath = Path.Combine(appFolder, "cache.db");
        _filesPath = Path.Combine(appFolder, "files");
        Directory.CreateDirectory(_filesPath);
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Documents (
                Id TEXT PRIMARY KEY,
                DocCode TEXT,
                Title TEXT,
                TypeName TEXT,
                Area TEXT,
                Process TEXT,
                FolderId TEXT,
                Status TEXT,
                NextReviewDue TEXT,
                CreatedAt TEXT,
                CurrentVersionLabel TEXT
            );";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task CacheDocumentsAsync(IEnumerable<DocumentDto> documents)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        
        // Clear old cache for simplicity in MVP
        using (var deleteCmd = new SqliteCommand("DELETE FROM Documents", connection, transaction))
        {
            await deleteCmd.ExecuteNonQueryAsync();
        }

        foreach (var doc in documents)
        {
            var insertSql = @"
                INSERT INTO Documents (Id, DocCode, Title, TypeName, Area, Process, FolderId, Status, NextReviewDue, CreatedAt, CurrentVersionLabel)
                VALUES ($id, $code, $title, $type, $area, $process, $folder, $status, $review, $created, $version);";

            using var insertCmd = new SqliteCommand(insertSql, connection, transaction);
            insertCmd.Parameters.AddWithValue("$id", doc.Id.ToString());
            insertCmd.Parameters.AddWithValue("$code", doc.DocCode);
            insertCmd.Parameters.AddWithValue("$title", doc.Title);
            insertCmd.Parameters.AddWithValue("$type", doc.TypeName ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("$area", doc.Area ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("$process", doc.Process ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("$folder", doc.FolderId?.ToString() ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("$status", doc.Status.ToString());
            insertCmd.Parameters.AddWithValue("$review", doc.NextReviewDue?.ToString("O") ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("$created", doc.CreatedAt.ToString("O"));
            insertCmd.Parameters.AddWithValue("$version", doc.CurrentVersionLabel ?? (object)DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<IEnumerable<DocumentDto>> GetCachedDocumentsAsync()
    {
        var result = new List<DocumentDto>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = new SqliteCommand("SELECT * FROM Documents", connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new DocumentDto(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                Enum.Parse<DocumentStatus>(reader.GetString(7)),
                reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                DateTime.Parse(reader.GetString(9)),
                reader.IsDBNull(10) ? null : reader.GetString(10)
            ));
        }

        return result;
    }

    public async Task SaveDocumentContentAsync(Guid documentId, Stream content, string fileName)
    {
        var filePath = Path.Combine(_filesPath, $"{documentId}_{fileName}");
        using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream);
    }

    public Task<string?> GetCachedFilePathAsync(Guid documentId)
    {
        var files = Directory.GetFiles(_filesPath, $"{documentId}_*");
        return Task.FromResult(files.Length > 0 ? files[0] : null);
    }
}
