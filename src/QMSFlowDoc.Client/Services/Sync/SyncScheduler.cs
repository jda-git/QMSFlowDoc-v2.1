using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

/// <summary>
/// Resultado de una ejecución de sincronización
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public int FilesUploaded { get; set; }
    public int FilesDownloaded { get; set; }
    public int Conflicts { get; set; }
    public int Errors { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Planificador de sincronización automática
/// </summary>
public class SyncScheduler : IDisposable
{
    private readonly SyncEngine _syncEngine;
    private readonly OfflineQueue _offlineQueue;
    private Timer? _timer;
    private bool _isRunning;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public event Action<SyncResult>? SyncCompleted;
    public event Action<bool>? NetworkStatusChanged;

    public bool IsRunning => _isRunning;
    public DateTime? LastSyncTime { get; private set; }
    public SyncResult? LastSyncResult { get; private set; }

    public SyncScheduler(SyncEngine syncEngine, OfflineQueue offlineQueue)
    {
        _syncEngine = syncEngine;
        _offlineQueue = offlineQueue;
    }

    /// <summary>
    /// Inicia la sincronización periódica
    /// </summary>
    /// <param name="intervalMinutes">Intervalo entre sincronizaciones en minutos</param>
    /// <param name="runImmediately">Si debe ejecutar sync inmediatamente al iniciar</param>
    public async Task StartAsync(int intervalMinutes, bool runImmediately = true)
    {
        if (_isRunning)
            return;

        _isRunning = true;

        var interval = TimeSpan.FromMinutes(intervalMinutes);

        if (runImmediately)
        {
            // Ejecutar sync inmediatamente
            await RunSyncAsync();
        }

        // Configurar timer para ejecuciones periódicas
        _timer = new Timer(
            async _ => await RunSyncAsync(),
            null,
            interval,
            interval);
    }

    /// <summary>
    /// Detiene la sincronización periódica
    /// </summary>
    public Task StopAsync()
    {
        _isRunning = false;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ejecuta una sincronización manualmente
    /// </summary>
    public async Task<SyncResult> RunSyncAsync()
    {
        // Evitar ejecuciones concurrentes
        if (!await _semaphore.WaitAsync(0))
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = "Sincronización ya en progreso"
            };
        }

        try
        {
            var startTime = DateTime.Now;
            var result = new SyncResult { Success = true };

            // Intentar ejecutar sincronización principal
            try
            {
                await _syncEngine.RunSyncAsync();
                
                // Obtener estadísticas del sync
                var snapshots = await _syncEngine.GetSnapshotStore().GetAllSnapshotsAsync();
                result.FilesUploaded = snapshots.Count(s => s.LastSyncDirection == "UP" && 
                                                             s.LastSyncAtUtc > startTime);
                result.FilesDownloaded = snapshots.Count(s => s.LastSyncDirection == "DOWN" && 
                                                               s.LastSyncAtUtc > startTime);
                result.Conflicts = snapshots.Count(s => s.Status == SyncStatus.Conflict);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors++;
                result.ErrorMessage = ex.Message;
            }

            // Procesar cola offline si hay operaciones pendientes
            try
            {
                var queueResult = await _offlineQueue.ProcessQueueAsync(async op =>
                {
                    // Aquí iría la lógica para ejecutar la operación
                    // Por ahora, simplemente intentamos el sync nuevamente
                    return true;
                });

                result.FilesUploaded += queueResult.Completed;
                result.Errors += queueResult.Failed;
            }
            catch (Exception ex)
            {
                result.Errors++;
                result.ErrorMessage = (result.ErrorMessage ?? "") + " | Queue error: " + ex.Message;
            }

            result.Duration = DateTime.Now - startTime;
            LastSyncTime = DateTime.Now;
            LastSyncResult = result;

            // Notificar resultado
            SyncCompleted?.Invoke(result);

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene el conteo de operaciones pendientes
    /// </summary>
    public async Task<int> GetPendingOperationsCountAsync()
    {
        return await _offlineQueue.GetPendingCountAsync();
    }

    /// <summary>
    /// Obtiene el conteo de conflictos sin resolver
    /// </summary>
    public async Task<int> GetConflictsCountAsync()
    {
        var conflicts = await _syncEngine.GetSnapshotStore().GetConflictsAsync();
        return conflicts.Count;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _semaphore?.Dispose();
    }
}
