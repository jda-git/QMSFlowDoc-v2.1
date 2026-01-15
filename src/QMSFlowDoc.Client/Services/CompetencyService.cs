using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface ICompetencyService
{
    Task<IEnumerable<CompetencyDto>> GetCatalogAsync();
    Task<IEnumerable<CompetencyEvaluationDto>> GetStaffEvaluationsAsync(Guid staffId);
    Task<bool> DeleteEvaluationAsync(Guid evaluationId);
}

public class CompetencyService : ICompetencyService
{
    private readonly HttpClient _httpClient;

    public async Task<IEnumerable<CompetencyDto>> GetCatalogAsync()
    {
        // For now, no local catalog, return empty if offline
         try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<CompetencyDto>>("competencies/catalog")
                   ?? new List<CompetencyDto>();
        }
        catch { return new List<CompetencyDto>(); }
    }

    public async Task<IEnumerable<CompetencyEvaluationDto>> GetStaffEvaluationsAsync(Guid staffId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<CompetencyEvaluationDto>>($"competencies/staff/{staffId}")
                   ?? new List<CompetencyEvaluationDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetStaffEvaluationsAsync(staffId);
        }
    }

    public async Task<bool> DeleteEvaluationAsync(Guid evaluationId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"competencies/evaluation/{evaluationId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteEvaluationAsync(evaluationId);
        }
    }

    // Needed for accessing local store
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public CompetencyService(HttpClient httpClient, NetworkConfigStore networkConfig)
    {
        _httpClient = httpClient;
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
}
