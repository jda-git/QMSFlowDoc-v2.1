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
    Task<bool> DeleteReagentAsync(Guid id, string password);
    Task<List<InventoryMovementDto>> GetMovementsAsync(DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId);
}

public class InventoryService : IInventoryService
{
    private readonly HttpClient _httpClient;

    public InventoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null)
    {
        var url = "inventory/reagents?";
        if (isActive.HasValue) url += $"isActive={isActive.Value}&";
        if (isLowStock.HasValue) url += $"isLowStock={isLowStock.Value}&";

        return await _httpClient.GetFromJsonAsync<IEnumerable<ReagentListDto>>(url)
               ?? new List<ReagentListDto>();
    }

    public async Task<Reagent?> GetReagentByIdAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<Reagent>($"inventory/reagents/{id}");
    }

    public async Task<Reagent?> CreateReagentAsync(CreateReagentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("inventory/reagents", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Reagent>() : null;
    }

    public async Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"inventory/reagents/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateReagentStatusAsync(Guid id, int status)
    {
        var response = await _httpClient.PatchAsync($"inventory/reagents/{id}/status", JsonContent.Create(status));
        return response.IsSuccessStatusCode;
    }

    public async Task<List<ReagentLot>?> RegisterLotAsync(RegisterLotRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("inventory/lots", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<List<ReagentLot>>() : null;
    }

    public async Task<bool> DeleteReagentAsync(Guid id, string password)
    {
        var response = await _httpClient.DeleteAsync($"inventory/reagents/{id}?password={Uri.EscapeDataString(password)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AdjustStockAsync(AdjustStockRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("inventory/adjust", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<InventoryMovementDto>> GetMovementsAsync(DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId)
    {
        var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
        if (from.HasValue) queryString["from"] = from.Value.ToString("o");
        if (to.HasValue) queryString["to"] = to.Value.ToString("o");
        if (type.HasValue) queryString["type"] = ((int)type.Value).ToString(); // Or enum string? API expects int usually or string if configured. Default is int.
        if (reagentId.HasValue) queryString["reagentId"] = reagentId.Value.ToString();

        var url = $"inventory/movements?{queryString}";
        return await _httpClient.GetFromJsonAsync<List<InventoryMovementDto>>(url) ?? new List<InventoryMovementDto>();
    }
}
