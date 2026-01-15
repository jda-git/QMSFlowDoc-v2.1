using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IInventoryService
{
    Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null);
    Task<Reagent?> GetReagentByIdAsync(Guid id);
    Task<Reagent?> CreateReagentAsync(CreateReagentRequest request);
    Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request);
    Task<bool> UpdateReagentStatusAsync(Guid id, int status);
    Task<List<ReagentLot>?> RegisterLotAsync(RegisterLotRequest request);
    Task<bool> AdjustStockAsync(AdjustStockRequest request);
    Task<bool> DeleteReagentAsync(Guid id);
    Task<List<InventoryMovementDto>> GetMovementsAsync(DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId);
}

public class InventoryService : IInventoryService
{
    private readonly HttpClient _httpClient;
    private LocalDocumentStore? _localStore;
    private readonly NetworkConfigStore _networkConfig;
    private bool _useLocalMode = false;

    public InventoryService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
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

    public async Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null)
    {
        try
        {
            var url = "inventory/reagents?";
            if (isActive.HasValue) url += $"isActive={isActive.Value}&";
            if (isLowStock.HasValue) url += $"isLowStock={isLowStock.Value}&";
            var reagents = await _httpClient.GetFromJsonAsync<IEnumerable<ReagentListDto>>(url) ?? new List<ReagentListDto>();
            // Optional: Background update local store
            return reagents;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetReagentsAsync(isActive, isLowStock);
        }
    }

    public async Task<Reagent?> GetReagentByIdAsync(Guid id)
    {
        try { return await _httpClient.GetFromJsonAsync<Reagent>($"inventory/reagents/{id}"); }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetReagentByIdAsync(id);
        }
    }

    public async Task<Reagent?> CreateReagentAsync(CreateReagentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("inventory/reagents", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Reagent>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var reagent = new Reagent
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Manufacturer = request.Manufacturer,
                ReagentType = request.ReagentType,
                Reference = request.Reference,
                Classification = request.Classification,
                StorageConditions = request.StorageConditions,
                OpenShelfLifeDays = request.OpenShelfLifeDays,
                MinStock = request.MinStock,
                TargetStock = request.TargetStock,
                ReorderQty = request.ReorderQty,
                Status = ReagentStatus.ACTIVO,
                CreatedAt = DateTime.UtcNow,
                Fluorescence = request.Fluorescence,
                ManufacturerCode = request.ManufacturerCode,
                InternalCode = request.InternalCode
            };
            await store.CreateReagentAsync(reagent);
            return reagent;
        }
    }

    public async Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"inventory/reagents/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.UpdateReagentAsync(id, request);
        }
    }

    public async Task<bool> UpdateReagentStatusAsync(Guid id, int status)
    {
        try
        {
            var response = await _httpClient.PatchAsync($"inventory/reagents/{id}/status", JsonContent.Create(status));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.UpdateReagentStatusAsync(id, status);
        }
    }

    public async Task<List<ReagentLot>?> RegisterLotAsync(RegisterLotRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("inventory/lots", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<ReagentLot>>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.RegisterLotAsync(request);
        }
    }

    public async Task<bool> DeleteReagentAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"inventory/reagents/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteReagentAsync(id);
        }
    }

    public async Task<bool> AdjustStockAsync(AdjustStockRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("inventory/adjust", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.AdjustStockAsync(request);
        }
    }

    public async Task<List<InventoryMovementDto>> GetMovementsAsync(DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId)
    {
        try
        {
            var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            if (from.HasValue) queryString["from"] = from.Value.ToString("o");
            if (to.HasValue) queryString["to"] = to.Value.ToString("o");
            if (type.HasValue) queryString["type"] = ((int)type.Value).ToString();
            if (reagentId.HasValue) queryString["reagentId"] = reagentId.Value.ToString();
            return await _httpClient.GetFromJsonAsync<List<InventoryMovementDto>>($"inventory/movements?{queryString}") 
                   ?? new List<InventoryMovementDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetMovementsAsync(from, to, type, reagentId);
        }
    }
}
