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

    public QualityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<NCListDto>> GetNonconformitiesAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<NCListDto>>("quality/nc")
               ?? new List<NCListDto>();
    }

    public async Task<Nonconformity?> GetNCByIdAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<Nonconformity>($"quality/nc/{id}");
    }

    public async Task<Nonconformity?> CreateNCAsync(CreateNCRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("quality/nc", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Nonconformity>() : null;
    }

    public async Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"quality/nc/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateNCStatusAsync(Guid id, int status)
    {
        var response = await _httpClient.PatchAsync($"quality/nc/{id}/status", JsonContent.Create(status));
        return response.IsSuccessStatusCode;
    }

    public async Task<CapaAction?> CreateCAPAAsync(CreateCAPARequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("quality/capa", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CapaAction>() : null;
    }
}
