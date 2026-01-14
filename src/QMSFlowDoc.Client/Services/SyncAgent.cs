using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public class SyncAgent
{
    private readonly IDocumentService _documentService;
    private readonly ILocalCacheService _cacheService;
    private readonly Timer _syncTimer;
    private bool _isSyncing = false;

    public event Action<string>? SyncStatusChanged;

    public SyncAgent(IDocumentService documentService, ILocalCacheService cacheService)
    {
        _documentService = documentService;
        _cacheService = cacheService;
        
        // Run sync every 5 minutes
        _syncTimer = new Timer(async _ => await PerformSyncAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
    }

    public async Task PerformSyncAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;
        SyncStatusChanged?.Invoke("Sincronizando...");

        try
        {
            // 1. Fetch remote documents
            var remoteDocs = await _documentService.GetDocumentsAsync();
            
            // 2. Fetch local documents
            var localDocs = await _cacheService.GetCachedDocumentsAsync();

            // 3. Detect Conflicts (Simplified)
            // In a real app, compare ETag/Hash/Timestamp
            foreach (var remoteDoc in remoteDocs)
            {
                var localDoc = localDocs.FirstOrDefault(d => d.Id == remoteDoc.Id);
                if (localDoc == null)
                {
                    // New document from server, should be cached automatically by DocumentService during fetch
                }
                else if (remoteDoc.CurrentVersionLabel != localDoc.CurrentVersionLabel)
                {
                    // Remote is newer, download?
                    SyncStatusChanged?.Invoke($"Actualizando {remoteDoc.DocCode}...");
                    // await _documentService.DownloadLatestVersionAsync(remoteDoc.Id); // Placeholder
                }
            }

            SyncStatusChanged?.Invoke("Sincronización completada.");
        }
        catch (Exception ex)
        {
            SyncStatusChanged?.Invoke($"Error de sincronización: {ex.Message}");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void Stop()
    {
        _syncTimer.Dispose();
    }
}
