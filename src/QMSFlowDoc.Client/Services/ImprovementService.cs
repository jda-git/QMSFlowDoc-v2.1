using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IImprovementService
{
    // Risks
    Task<IEnumerable<RiskListDto>> GetRisksAsync();
    Task<Risk?> GetRiskByIdAsync(Guid id);
    Task<Risk?> CreateRiskAsync(CreateRiskRequest request);
    Task<bool> UpdateRiskAsync(Guid id, CreateRiskRequest request);
    Task<bool> UpdateRiskStatusAsync(Guid id, int status);

    // Audits
    Task<IEnumerable<AuditListDto>> GetAuditsAsync();
    Task<AuditPlan?> GetAuditByIdAsync(Guid id);
    Task<AuditPlan?> CreateAuditAsync(CreateAuditRequest request);
    Task<bool> UpdateAuditAsync(Guid id, CreateAuditRequest request);
    Task<AuditFinding?> RegisterFindingAsync(RegisterFindingRequest request);

    // Reviews
    Task<IEnumerable<ManagementReviewListDto>> GetReviewsAsync();
    Task<ManagementReview?> GetReviewByIdAsync(Guid id);
    Task<ManagementReview?> CreateReviewAsync(CreateManagementReviewRequest request);
    Task<bool> UpdateReviewAsync(Guid id, CreateManagementReviewRequest request);
}

public class ImprovementService : IImprovementService
{
    private readonly HttpClient _httpClient;

    public ImprovementService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<RiskListDto>> GetRisksAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<RiskListDto>>("improvement/risks")
               ?? new List<RiskListDto>();
    }

    public async Task<Risk?> GetRiskByIdAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<Risk>($"improvement/risks/{id}");
    }

    public async Task<Risk?> CreateRiskAsync(CreateRiskRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("improvement/risks", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Risk>() : null;
    }

    public async Task<bool> UpdateRiskAsync(Guid id, CreateRiskRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"improvement/risks/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateRiskStatusAsync(Guid id, int status)
    {
        var response = await _httpClient.PatchAsync($"improvement/risks/{id}/status", JsonContent.Create(status));
        return response.IsSuccessStatusCode;
    }

    public async Task<IEnumerable<AuditListDto>> GetAuditsAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<AuditListDto>>("improvement/audits")
               ?? new List<AuditListDto>();
    }

    public async Task<AuditPlan?> GetAuditByIdAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<AuditPlan>($"improvement/audits/{id}");
    }

    public async Task<AuditPlan?> CreateAuditAsync(CreateAuditRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("improvement/audits", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AuditPlan>() : null;
    }

    public async Task<bool> UpdateAuditAsync(Guid id, CreateAuditRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"improvement/audits/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<AuditFinding?> RegisterFindingAsync(RegisterFindingRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("improvement/findings", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AuditFinding>() : null;
    }

    public async Task<IEnumerable<ManagementReviewListDto>> GetReviewsAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ManagementReviewListDto>>("improvement/reviews")
               ?? new List<ManagementReviewListDto>();
    }

    public async Task<ManagementReview?> GetReviewByIdAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<ManagementReview>($"improvement/reviews/{id}");
    }

    public async Task<ManagementReview?> CreateReviewAsync(CreateManagementReviewRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("improvement/reviews", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ManagementReview>() : null;
    }

    public async Task<bool> UpdateReviewAsync(Guid id, CreateManagementReviewRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"improvement/reviews/{id}", request);
        return response.IsSuccessStatusCode;
    }
}
