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

    public SearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
            return new List<SearchResultDto>();
        }
    }
}
