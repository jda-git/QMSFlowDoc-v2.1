using QMSFlowDoc.Shared.DTOs;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IDashboardService
{
    Task<DashboardDataDto?> GetDashboardDataAsync();
}

public class DashboardService : IDashboardService
{
    private readonly HttpClient _httpClient;
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public DashboardService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
    {
        _httpClient = httpClient;
        _networkConfig = networkConfig ?? new NetworkConfigStore();
        _localStore = localStore;
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

    public async Task<DashboardDataDto?> GetDashboardDataAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DashboardDataDto>("dashboard");
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetDashboardDataAsync();
        }
    }
}
