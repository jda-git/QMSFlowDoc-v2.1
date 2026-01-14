using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IEquipmentService
{
    Task<IEnumerable<EquipmentListDto>> GetEquipmentAsync();
    Task<Equipment?> GetEquipmentByIdAsync(Guid id);
    Task<Equipment?> CreateEquipmentAsync(CreateEquipmentRequest request);
    Task<Equipment?> UpdateEquipmentAsync(UpdateEquipmentRequest request);
    Task<MaintenanceEvent?> GetLastMaintenanceAsync(Guid equipmentId);
    Task<MaintenanceEvent?> RegisterMaintenanceAsync(RegisterMaintenanceRequest request);
    Task<MaintenanceEvent?> UpdateMaintenanceAsync(UpdateMaintenanceRequest request);
    Task<bool> DeleteEquipmentAsync(Guid id);
}

public class EquipmentService : IEquipmentService
{
    private readonly HttpClient _httpClient;

    public EquipmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<EquipmentListDto>> GetEquipmentAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<EquipmentListDto>>("equipment")
               ?? new List<EquipmentListDto>();
    }

    public async Task<Equipment?> GetEquipmentByIdAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<Equipment>($"equipment/{id}");
    }

    public async Task<Equipment?> CreateEquipmentAsync(CreateEquipmentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("equipment", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Equipment>() : null;
    }

    public async Task<Equipment?> UpdateEquipmentAsync(UpdateEquipmentRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"equipment/{request.Id}", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Equipment>() : null;
    }

    public async Task<MaintenanceEvent?> GetLastMaintenanceAsync(Guid equipmentId)
    {
        return await _httpClient.GetFromJsonAsync<MaintenanceEvent>($"equipment/{equipmentId}/maintenance/last");
    }

    public async Task<MaintenanceEvent?> RegisterMaintenanceAsync(RegisterMaintenanceRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("equipment/maintenance", request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(string.IsNullOrWhiteSpace(error) ? $"Server returned {response.StatusCode}" : error);
        }
        return await response.Content.ReadFromJsonAsync<MaintenanceEvent>();
    }

    public async Task<MaintenanceEvent?> UpdateMaintenanceAsync(UpdateMaintenanceRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"equipment/maintenance/{request.Id}", request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(string.IsNullOrWhiteSpace(error) ? $"Server returned {response.StatusCode}" : error);
        }
        return await response.Content.ReadFromJsonAsync<MaintenanceEvent>();
    }

    public async Task<bool> DeleteEquipmentAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"equipment/{id}");
        return response.IsSuccessStatusCode;
    }
}
