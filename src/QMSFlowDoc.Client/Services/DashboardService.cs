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

    public DashboardService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DashboardDataDto?> GetDashboardDataAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DashboardDataDto>("dashboard");
        }
        catch
        {
            return null;
        }
    }
}
