using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IQualityService
{
    Task<IEnumerable<NCListDto>> GetNonconformitiesAsync();
    Task<Nonconformity?> GetNCByIdAsync(Guid id);
    Task<Nonconformity?> CreateNCAsync(CreateNCRequest request);
    Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request);
    Task<bool> UpdateNCStatusAsync(Guid id, int status);
    Task<CapaAction?> CreateCAPAAsync(CreateCAPARequest request);
}

public class QualityService : IQualityService
{
    private readonly HttpClient _httpClient;
    private LocalDocumentStore? _localStore;
    private readonly NetworkConfigStore _networkConfig;
    private bool _useLocalMode = false;

    public QualityService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
    {
        _httpClient = httpClient;
        _localStore = localStore;
        _networkConfig = networkConfig ?? new NetworkConfigStore();
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

    public async Task<IEnumerable<NCListDto>> GetNonconformitiesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<NCListDto>>("quality/nc")
                   ?? new List<NCListDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetNonconformitiesAsync();
        }
    }

    public async Task<Nonconformity?> GetNCByIdAsync(Guid id)
    {
        try { return await _httpClient.GetFromJsonAsync<Nonconformity>($"quality/nc/{id}"); }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetNCByIdAsync(id);
        }
    }

    public async Task<Nonconformity?> CreateNCAsync(CreateNCRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("quality/nc", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Nonconformity>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var nc = new Nonconformity
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description,
                Severity = request.Severity,
                Status = request.Status ?? NCStatus.OPEN,
                ImpactPatient = request.ImpactPatient,
                Containment = request.Containment,
                Origin = request.Origin,
                RootCauseAnalysis = request.RootCauseAnalysis,
                DetectedAt = DateTime.UtcNow,
                DetectedByUserId = request.DetectedByUserId,
                UpdatedAt = DateTime.UtcNow
            };
            await store.CreateNCAsync(nc);
            return nc;
        }
    }

    public async Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"quality/nc/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.UpdateNCAsync(id, request);
            return true;
        }
    }

    public async Task<bool> UpdateNCStatusAsync(Guid id, int status)
    {
        try
        {
            var response = await _httpClient.PatchAsync($"quality/nc/{id}/status", JsonContent.Create(status));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.UpdateNCStatusAsync(id, status);
            return true;
        }
    }

    public async Task<CapaAction?> CreateCAPAAsync(CreateCAPARequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("quality/capa", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CapaAction>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var action = new CapaAction
            {
                Id = Guid.NewGuid(),
                NCId = request.NCId,
                ActionType = request.ActionType,
                Description = request.Description,
                OwnerUserId = request.OwnerUserId,
                DueDate = request.DueDate,
                Status = CAPAStatus.OPEN
            };
            await store.CreateCAPAAsync(action);
            return action;
        }
    }
}
