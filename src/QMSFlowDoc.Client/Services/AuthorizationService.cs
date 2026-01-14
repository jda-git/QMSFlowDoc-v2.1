using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IAuthorizationService
{
    Task<IEnumerable<AuthorizationDto>> GetCatalogAsync();
    Task<IEnumerable<StaffAuthorizationDto>> GetStaffAuthorizationsAsync(Guid staffId);
    Task<bool> GrantAuthorizationAsync(GrantAuthorizationRequest request);
    Task<bool> DeleteAuthorizationAsync(Guid authorizationId);
}

public class AuthorizationService : IAuthorizationService
{
    private readonly HttpClient _httpClient;

    public AuthorizationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<AuthorizationDto>> GetCatalogAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<AuthorizationDto>>("authorizations/catalog")
               ?? new List<AuthorizationDto>();
    }

    public async Task<IEnumerable<StaffAuthorizationDto>> GetStaffAuthorizationsAsync(Guid staffId)
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<StaffAuthorizationDto>>($"authorizations/staff/{staffId}")
               ?? new List<StaffAuthorizationDto>();
    }

    public async Task<bool> GrantAuthorizationAsync(GrantAuthorizationRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("authorizations/grant", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAuthorizationAsync(Guid authorizationId)
    {
        var response = await _httpClient.DeleteAsync($"authorizations/{authorizationId}");
        return response.IsSuccessStatusCode;
    }
}
