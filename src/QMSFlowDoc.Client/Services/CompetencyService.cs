using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface ICompetencyService
{
    Task<IEnumerable<Competency>> GetCatalogAsync();
    Task<Competency?> GetCompetencyByIdAsync(Guid id);
    Task UpsertCompetencyAsync(Competency competency);
    Task<bool> DeleteCompetencyAsync(Guid id);

    Task<IEnumerable<CompetencyEvaluationDto>> GetStaffEvaluationsAsync(Guid staffId);
    Task UpsertEvaluationAsync(CompetencyEvaluationDto dto);
    Task<bool> DeleteEvaluationAsync(Guid evaluationId);
}

public class CompetencyService : ICompetencyService
{
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public CompetencyService(NetworkConfigStore networkConfig)
    {
        _networkConfig = networkConfig;
    }

    private async Task<LocalDocumentStore> GetLocalStoreAsync()
    {
        if (_localStore == null)
        {
            _localStore = new LocalDocumentStore(_networkConfig);
            await _localStore.InitializeAsync();
        }
        return _localStore;
    }

    public async Task<IEnumerable<Competency>> GetCatalogAsync()
    {
        var store = await GetLocalStoreAsync();
        return await store.GetCompetenciesAsync();
    }

    public async Task<Competency?> GetCompetencyByIdAsync(Guid id)
    {
        var store = await GetLocalStoreAsync();
        return await store.GetCompetencyByIdAsync(id);
    }

    public async Task UpsertCompetencyAsync(Competency competency)
    {
        var store = await GetLocalStoreAsync();
        await store.UpsertCompetencyAsync(competency);
    }

    public async Task<bool> DeleteCompetencyAsync(Guid id)
    {
        var store = await GetLocalStoreAsync();
        return await store.DeleteCompetencyAsync(id);
    }

    public async Task<IEnumerable<CompetencyEvaluationDto>> GetStaffEvaluationsAsync(Guid staffId)
    {
        var store = await GetLocalStoreAsync();
        return await store.GetStaffEvaluationsAsync(staffId);
    }

    public async Task UpsertEvaluationAsync(CompetencyEvaluationDto dto)
    {
        var store = await GetLocalStoreAsync();
        await store.UpsertEvaluationAsync(dto);
    }

    public async Task<bool> DeleteEvaluationAsync(Guid evaluationId)
    {
        var store = await GetLocalStoreAsync();
        return await store.DeleteEvaluationAsync(evaluationId);
    }
}
