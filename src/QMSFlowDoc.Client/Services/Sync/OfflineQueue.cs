using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

/// <summary>
/// Tipo de operación pendiente
/// </summary>
public enum OperationType
{
    Upload,
    Download,
    Delete,
    Move,
    Rename
}

/// <summary>
/// Operación pendiente cuando la red no está disponible
/// </summary>
public class PendingOperation
{
    public int Id { get; set; }
    public OperationType Type { get; set; }
    public string SourcePath { get; set; } = "";
    public string? DestinationPath { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastRetryAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Cola de operaciones pendientes para modo offline
/// </summary>
public class OfflineQueue
{
    private readonly string _dbPath;

    public OfflineQueue()
    {
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "offline_queue.db");
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS PendingOperations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Type INTEGER NOT NULL,
                SourcePath TEXT NOT NULL,
                DestinationPath TEXT,
                EnqueuedAt TEXT NOT NULL,
                RetryCount INTEGER DEFAULT 0,
                LastRetryAt TEXT,
                ErrorMessage TEXT
            );";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Encola una operación para ejecutar cuando la red esté disponible
    /// </summary>
    public async Task<int> EnqueueAsync(OperationType type, string sourcePath, string? destinationPath = null)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO PendingOperations (Type, SourcePath, DestinationPath, EnqueuedAt, RetryCount)
            VALUES ($type, $source, $dest, $enqueued, 0);
            SELECT last_insert_rowid();";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$type", (int)type);
        command.Parameters.AddWithValue("$source", sourcePath);
        command.Parameters.AddWithValue("$dest", destinationPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$enqueued", DateTime.UtcNow.ToString("O"));

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Obtiene todas las operaciones pendientes
    /// </summary>
    public async Task<List<PendingOperation>> GetPendingAsync()
    {
        var list = new List<PendingOperation>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = new SqliteCommand("SELECT * FROM PendingOperations ORDER BY EnqueuedAt ASC", connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            list.Add(ReadOperation(reader));
        }
        
        return list;
    }

    /// <summary>
    /// Obtiene el conteo de operaciones pendientes
    /// </summary>
    public async Task<int> GetPendingCountAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = new SqliteCommand("SELECT COUNT(*) FROM PendingOperations", connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Marca una operación como completada y la elimina de la cola
    /// </summary>
    public async Task CompleteAsync(int operationId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        using var command = new SqliteCommand("DELETE FROM PendingOperations WHERE Id = $id", connection);
        command.Parameters.AddWithValue("$id", operationId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Incrementa el contador de reintentos de una operación
    /// </summary>
    public async Task IncrementRetryAsync(int operationId, string? errorMessage = null)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            UPDATE PendingOperations 
            SET RetryCount = RetryCount + 1, 
                LastRetryAt = $lastRetry,
                ErrorMessage = $error
            WHERE Id = $id";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", operationId);
        command.Parameters.AddWithValue("$lastRetry", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$error", errorMessage ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Calcula el delay de backoff exponencial basado en el número de reintentos
    /// </summary>
    public TimeSpan CalculateBackoffDelay(int retryCount)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 32s, max 60s
        var delaySeconds = Math.Min(Math.Pow(2, retryCount), 60);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    /// <summary>
    /// Procesa la cola de operaciones pendientes
    /// </summary>
    public async Task<ProcessQueueResult> ProcessQueueAsync(
        Func<PendingOperation, Task<bool>> executor,
        int maxRetries = 5)
    {
        var operations = await GetPendingAsync();
        var result = new ProcessQueueResult();

        foreach (var op in operations)
        {
            // Si ha superado el máximo de reintentos, omitir
            if (op.RetryCount >= maxRetries)
            {
                result.Skipped++;
                continue;
            }

            // Calcular delay de backoff
            if (op.RetryCount > 0 && op.LastRetryAt.HasValue)
            {
                var backoffDelay = CalculateBackoffDelay(op.RetryCount);
                var nextRetryTime = op.LastRetryAt.Value.Add(backoffDelay);
                
                if (DateTime.UtcNow < nextRetryTime)
                {
                    // Aún no es momento de reintentar
                    result.Skipped++;
                    continue;
                }
            }

            try
            {
                // Ejecutar operación
                var success = await executor(op);
                
                if (success)
                {
                    await CompleteAsync(op.Id);
                    result.Completed++;
                }
                else
                {
                    await IncrementRetryAsync(op.Id, "Operation failed");
                    result.Failed++;
                }
            }
            catch (Exception ex)
            {
                await IncrementRetryAsync(op.Id, ex.Message);
                result.Failed++;
            }
        }

        return result;
    }

    private PendingOperation ReadOperation(SqliteDataReader reader)
    {
        return new PendingOperation
        {
            Id = reader.GetInt32(0),
            Type = (OperationType)reader.GetInt32(1),
            SourcePath = reader.GetString(2),
            DestinationPath = reader.IsDBNull(3) ? null : reader.GetString(3),
            EnqueuedAt = DateTime.Parse(reader.GetString(4)),
            RetryCount = reader.GetInt32(5),
            LastRetryAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
            ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}

/// <summary>
/// Resultado del procesamiento de la cola
/// </summary>
public class ProcessQueueResult
{
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}
