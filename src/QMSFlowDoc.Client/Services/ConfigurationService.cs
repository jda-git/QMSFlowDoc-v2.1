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

    public async Task<IEnumerable<ReagentType>> GetReagentTypesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<ReagentType>>("configuration/reagent-types")
                   ?? new List<ReagentType>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetReagentTypesAsync();
        }
    }

    public async Task<ReagentType?> CreateReagentTypeAsync(ReagentType type)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("configuration/reagent-types", type);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ReagentType>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.CreateReagentTypeAsync(type);
        }
    }

    public async Task<bool> DeleteReagentTypeAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"configuration/reagent-types/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteReagentTypeAsync(id);
        }
    }

    public async Task<SystemSetting?> GetSettingAsync(string key)
    {
         try {
             return await _httpClient.GetFromJsonAsync<SystemSetting>($"configuration/settings/{key}");
         } catch { return null; }
    }

    public async Task<bool> UpdateSettingAsync(SystemSetting setting)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"configuration/settings/{setting.Key}", setting);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
    
    // Needed for accessing local store
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public ConfigurationService(HttpClient httpClient, NetworkConfigStore networkConfig)
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
}
