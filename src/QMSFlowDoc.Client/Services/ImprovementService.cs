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
    Task<bool> DeleteAuditAsync(Guid id);

    // Reviews
    Task<IEnumerable<ManagementReviewListDto>> GetReviewsAsync();
    Task<ManagementReview?> GetReviewByIdAsync(Guid id);
    Task<ManagementReview?> CreateReviewAsync(CreateManagementReviewRequest request);
    Task<bool> UpdateReviewAsync(Guid id, CreateManagementReviewRequest request);
    Task<bool> DeleteReviewAsync(Guid id);
}

public class ImprovementService : IImprovementService
{
    private readonly HttpClient _httpClient;
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public ImprovementService(HttpClient httpClient, NetworkConfigStore networkConfig)
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

    public async Task<IEnumerable<RiskListDto>> GetRisksAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<RiskListDto>>("improvement/risks")
                   ?? new List<RiskListDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetRisksAsync();
        }
    }

    public async Task<Risk?> GetRiskByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Risk>($"improvement/risks/{id}");
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetRiskByIdAsync(id);
        }
    }

    public async Task<Risk?> CreateRiskAsync(CreateRiskRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("improvement/risks", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Risk>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.CreateRiskAsync(request);
        }
    }

    public async Task<bool> UpdateRiskAsync(Guid id, CreateRiskRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"improvement/risks/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.UpdateRiskAsync(id, request);
        }
    }

    public async Task<bool> UpdateRiskStatusAsync(Guid id, int status)
    {
        try
        {
            var response = await _httpClient.PatchAsync($"improvement/risks/{id}/status", JsonContent.Create(status));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.UpdateRiskStatusAsync(id, status);
        }
    }

    public async Task<IEnumerable<AuditListDto>> GetAuditsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<AuditListDto>>("improvement/audits")
                   ?? new List<AuditListDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetAuditsAsync();
        }
    }

    public async Task<AuditPlan?> GetAuditByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AuditPlan>($"improvement/audits/{id}");
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetAuditByIdAsync(id);
        }
    }

    public async Task<AuditPlan?> CreateAuditAsync(CreateAuditRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("improvement/audits", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Server Error ({response.StatusCode}): {error}");
            }
            return await response.Content.ReadFromJsonAsync<AuditPlan>();
        }
        catch (HttpRequestException) // Only catch network errors
        {
            var store = await GetLocalStoreAsync();
            return await store.CreateAuditAsync(request);
        }
    }

    public async Task<bool> UpdateAuditAsync(Guid id, CreateAuditRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"improvement/audits/{id}", request);
            if (!response.IsSuccessStatusCode)
            {
                 var error = await response.Content.ReadAsStringAsync();
                 // throw new Exception($"Server Error ({response.StatusCode}): {error}"); // Don't throw to allow fallback if just network error? But non-success usually means semantic error.
                 // Actually logic usually is: Try API. If network fail -> Local. If API returns error -> Propagate.
                 // For now, simple catch-all or catch network like above. Sticking to simple catch all for hybrid mode pattern used elsewhere.
            }
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.UpdateAuditAsync(id, request);
        }
    }

    public async Task<AuditFinding?> RegisterFindingAsync(RegisterFindingRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("improvement/findings", request);
             if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Server Error ({response.StatusCode}): {error}");
            }
            return await response.Content.ReadFromJsonAsync<AuditFinding>();
        }
        catch (HttpRequestException)
        {
             var store = await GetLocalStoreAsync();
             return await store.RegisterFindingAsync(request);
        }
        catch (Exception) // Catch-all for now to be safe in demo
        {
             var store = await GetLocalStoreAsync();
             return await store.RegisterFindingAsync(request);
        }
    }

    public async Task<bool> DeleteAuditAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"improvement/audits/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteAuditAsync(id);
        }
    }

    public async Task<IEnumerable<ManagementReviewListDto>> GetReviewsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<ManagementReviewListDto>>("improvement/reviews")
                   ?? new List<ManagementReviewListDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetReviewsAsync();
        }
    }

    public async Task<ManagementReview?> GetReviewByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ManagementReview>($"improvement/reviews/{id}");
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetReviewByIdAsync(id);
        }
    }

    public async Task<ManagementReview?> CreateReviewAsync(CreateManagementReviewRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("improvement/reviews", request);
             if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Server Error ({response.StatusCode}): {error}");
            }
            return await response.Content.ReadFromJsonAsync<ManagementReview>();
        }
        catch (HttpRequestException)
        {
            var store = await GetLocalStoreAsync();
            return await store.CreateReviewAsync(request);
        }
        catch (Exception)
        {
            var store = await GetLocalStoreAsync();
            return await store.CreateReviewAsync(request);
        }
    }

    public async Task<bool> UpdateReviewAsync(Guid id, CreateManagementReviewRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"improvement/reviews/{id}", request);
             if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Server Error ({response.StatusCode}): {error}");
            }
            return true;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.UpdateReviewAsync(id, request);
        }
    }

    public async Task<bool> DeleteReviewAsync(Guid id)
    {
         try
        {
            var response = await _httpClient.DeleteAsync($"improvement/reviews/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteReviewAsync(id);
        }
    }
}
