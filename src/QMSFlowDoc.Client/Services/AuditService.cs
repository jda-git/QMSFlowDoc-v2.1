using QMSFlowDoc.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IAuditService
{
    Task<List<AuditLogDto>> GetLogsAsync(AuditFilter filter);
}

public class AuditService : IAuditService
{
    private readonly LocalDocumentStore _store;

    public AuditService(LocalDocumentStore store)
    {
        _store = store;
    }

    public async Task<List<AuditLogDto>> GetLogsAsync(AuditFilter filter)
    {
        return await _store.GetAuditLogsAsync(filter);
    }
}
