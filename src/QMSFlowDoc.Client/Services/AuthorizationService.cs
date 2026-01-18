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
    Task<IEnumerable<GlobalAuthorizationDto>> GetAllAuthorizationsAsync(string? staffName = null, string? status = null);
    Task<bool> GrantAuthorizationAsync(GrantAuthorizationRequest request);
    Task<bool> DeleteAuthorizationAsync(Guid authorizationId);
}

public class AuthorizationService : IAuthorizationService
{
    private readonly HttpClient _httpClient;

    public async Task<IEnumerable<AuthorizationDto>> GetCatalogAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<AuthorizationDto>>("authorizations/catalog")
                   ?? new List<AuthorizationDto>();
        }
        catch { return new List<AuthorizationDto>(); }
    }

    public async Task<IEnumerable<StaffAuthorizationDto>> GetStaffAuthorizationsAsync(Guid staffId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<StaffAuthorizationDto>>($"authorizations/staff/{staffId}")
                   ?? new List<StaffAuthorizationDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetStaffAuthorizationsAsync(staffId);
        }
    }

    public async Task<IEnumerable<GlobalAuthorizationDto>> GetAllAuthorizationsAsync(string? staffName = null, string? status = null)
    {
        try
        {
           throw new NotImplementedException();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetAllAuthorizationsAsync(staffName, status);
        }
    }

    public async Task<bool> GrantAuthorizationAsync(GrantAuthorizationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("authorizations/grant", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.GrantAuthorizationAsync(request);
            return true;
        }
    }

    public async Task<bool> DeleteAuthorizationAsync(Guid authorizationId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"authorizations/{authorizationId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteAuthorizationAsync(authorizationId);
        }
    }

    // Needed for accessing local store
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public AuthorizationService(HttpClient httpClient, NetworkConfigStore networkConfig)
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
