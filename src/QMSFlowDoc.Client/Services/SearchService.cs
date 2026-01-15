using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface ISearchService
{
    Task<IEnumerable<SearchResultDto>> SearchAsync(string query);
}

public class SearchService : ISearchService
{
    private readonly HttpClient _httpClient;
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public SearchService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
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

    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string query)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<SearchResultDto>>($"search?q={Uri.EscapeDataString(query)}")
                   ?? new List<SearchResultDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.SearchAsync(query);
        }
    }
}
