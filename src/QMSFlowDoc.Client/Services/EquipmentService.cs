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
    Task<bool> RegisterDailyQCAsync(CreateDailyQCRequest request);
    Task<IEnumerable<EquipmentDailyQCDto>> GetDailyQCAsync(Guid equipmentId);
}

public class EquipmentService : IEquipmentService
{
    private readonly HttpClient _httpClient;
    private LocalDocumentStore? _localStore;
    private readonly NetworkConfigStore _networkConfig;
    private bool _useLocalMode = false;

    public EquipmentService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
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

    public async Task<IEnumerable<EquipmentListDto>> GetEquipmentAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<EquipmentListDto>>("equipment")
                   ?? new List<EquipmentListDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetEquipmentAsync();
        }
    }

    public async Task<Equipment?> GetEquipmentByIdAsync(Guid id)
    {
        try { return await _httpClient.GetFromJsonAsync<Equipment>($"equipment/{id}"); }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetEquipmentByIdAsync(id);
        }
    }

    public async Task<Equipment?> CreateEquipmentAsync(CreateEquipmentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("equipment", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Equipment>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            // Map DTO to Model for local storage.
            var equipment = new Equipment
            {
                Id = Guid.NewGuid(),
                AssetTag = request.AssetTag,
                Name = request.Name,
                Manufacturer = request.Manufacturer,
                Model = request.Model,
                SerialNumber = request.SerialNumber,
                SoftwareVersion = request.SoftwareVersion,
                FirmwareVersion = request.FirmwareVersion,
                Location = request.Location,
                InstalledAt = request.InstalledAt,
                Status = EquipmentStatus.ACTIVE
            };
            await store.CreateEquipmentAsync(equipment);
            return equipment;
        }
    }

    public async Task<Equipment?> UpdateEquipmentAsync(UpdateEquipmentRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"equipment/{request.Id}", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Equipment>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.UpdateEquipmentAsync(request);
            return await store.GetEquipmentByIdAsync(request.Id);
        }
    }

    public async Task<MaintenanceEvent?> GetLastMaintenanceAsync(Guid equipmentId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<MaintenanceEvent>($"equipment/{equipmentId}/maintenance/last");
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetLastMaintenanceAsync(equipmentId);
        }
    }

    public async Task<MaintenanceEvent?> RegisterMaintenanceAsync(RegisterMaintenanceRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("equipment/maintenance", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MaintenanceEvent>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.RegisterMaintenanceAsync(request);
            // Construct a basic event for UI feedback
            return new MaintenanceEvent
            {
                Id = Guid.NewGuid(),
                EquipmentId = request.EquipmentId,
                PerformedAt = request.PerformedAt ?? DateTime.UtcNow,
                Notes = request.Notes
            };
        }
    }

    public async Task<MaintenanceEvent?> UpdateMaintenanceAsync(UpdateMaintenanceRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"equipment/maintenance/{request.Id}", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MaintenanceEvent>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.UpdateMaintenanceAsync(request);
            // Return updated object for UI
            return new MaintenanceEvent
            {
                Id = request.Id,
                EquipmentId = request.EquipmentId,
                PerformedAt = request.PerformedAt ?? DateTime.UtcNow,
                Notes = request.Notes,
                EventType = request.EventType,
                Outcome = request.Outcome
            };
        }
    }

    public async Task<bool> DeleteEquipmentAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"equipment/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteEquipmentAsync(id);
        }
    }

    public async Task<bool> RegisterDailyQCAsync(CreateDailyQCRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("equipment/qc", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.RegisterDailyQCAsync(request);
            return true;
        }
    }

    public async Task<IEnumerable<EquipmentDailyQCDto>> GetDailyQCAsync(Guid equipmentId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<EquipmentDailyQCDto>>($"equipment/{equipmentId}/qc")
                   ?? new List<EquipmentDailyQCDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetDailyQCAsync(equipmentId);
        }
    }
}
