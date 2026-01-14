using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IConfigurationService
{
    Task<IEnumerable<ReagentType>> GetReagentTypesAsync();
    Task<ReagentType?> CreateReagentTypeAsync(ReagentType type);
    Task<bool> DeleteReagentTypeAsync(Guid id);
    Task<SystemSetting?> GetSettingAsync(string key);
    Task<bool> UpdateSettingAsync(SystemSetting setting);
}

public class ConfigurationService : IConfigurationService
{
    private readonly HttpClient _httpClient;

    public ConfigurationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ReagentType>> GetReagentTypesAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<ReagentType>>("configuration/reagent-types")
               ?? new List<ReagentType>();
    }

    public async Task<ReagentType?> CreateReagentTypeAsync(ReagentType type)
    {
        var response = await _httpClient.PostAsJsonAsync("configuration/reagent-types", type);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ReagentType>() : null;
    }

    public async Task<bool> DeleteReagentTypeAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"configuration/reagent-types/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<SystemSetting?> GetSettingAsync(string key)
    {
         try {
             return await _httpClient.GetFromJsonAsync<SystemSetting>($"configuration/settings/{key}");
         } catch { return null; }
    }

    public async Task<bool> UpdateSettingAsync(SystemSetting setting)
    {
        var response = await _httpClient.PutAsJsonAsync($"configuration/settings/{setting.Key}", setting);
        return response.IsSuccessStatusCode;
    }
}
