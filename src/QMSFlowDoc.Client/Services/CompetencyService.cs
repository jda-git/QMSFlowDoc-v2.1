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

    public CompetencyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<CompetencyDto>> GetCatalogAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<CompetencyDto>>("competencies/catalog")
               ?? new List<CompetencyDto>();
    }

    public async Task<IEnumerable<CompetencyEvaluationDto>> GetStaffEvaluationsAsync(Guid staffId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<CompetencyEvaluationDto>>($"competencies/staff/{staffId}")
               ?? new List<CompetencyEvaluationDto>();
    }

    public async Task<bool> DeleteEvaluationAsync(Guid evaluationId)
    {
        var response = await _httpClient.DeleteAsync($"competencies/evaluation/{evaluationId}");
        return response.IsSuccessStatusCode;
    }
}
