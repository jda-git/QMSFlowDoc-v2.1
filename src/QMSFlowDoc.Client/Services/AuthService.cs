using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);
    Task<Guid?> RegisterAsync(RegisterRequest request);
    Task PurgeUsersAsync();
    Task<bool> ChangePasswordAsync(ChangePasswordRequest request);
    Task<bool> ResetPasswordAsync(Guid userId, ResetPasswordRequest request);
    Task<bool> UnlockAccountAsync(Guid userId);
    Task<bool> NeedsBootstrapAsync();
    Task<bool> BootstrapAsync(RegisterRequest request);
    void Logout();
    string? CurrentToken { get; }
    string? CurrentUsername { get; }
    Guid? CurrentUserId { get; }
    List<string> CurrentRoles { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private LocalDocumentStore? _localStore;
    private readonly NetworkConfigStore _networkConfig;
    
    public string? CurrentToken { get; private set; }
    public string? CurrentUsername { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public List<string> CurrentRoles { get; private set; } = new();
    public bool IsAuthenticated => !string.IsNullOrEmpty(CurrentToken) || !string.IsNullOrEmpty(CurrentUsername);
    public bool IsAdmin => CurrentRoles.Contains("Administrador");

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _networkConfig = new NetworkConfigStore();
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

    public async Task<bool> LoginAsync(string username, string password)
    {
        // First try API authentication
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", new LoginRequest(username, password));
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (result != null)
                {
                    CurrentToken = result.Token;
                    CurrentUsername = username;
                    CurrentUserId = null; // API mode doesn't give us ID easily yet
                    CurrentRoles = result.Roles ?? new List<string>();
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CurrentToken);
                    return true;
                }
            }
        }
        catch
        {
            // API not available, try local authentication
        }
        
        // Fallback to local SQLite authentication
        try
        {
            var localStore = await GetLocalStoreAsync();
            var (success, userId, fullName, role) = await localStore.ValidateUserAsync(username, password);
            
            if (success)
            {
                CurrentToken = $"local_{userId}"; // Pseudo-token for local mode
                CurrentUsername = username;
                CurrentUserId = Guid.Parse(userId);
                CurrentRoles = new List<string> { role };
                return true;
            }
        }
        catch
        {
            // Local auth also failed
        }
        
        return false;
    }

    public async Task<Guid?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                if (result.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                {
                    var idStr = result.GetProperty("id").GetString();
                    return idStr != null ? Guid.Parse(idStr) : null;
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(string.IsNullOrWhiteSpace(error) ? $"Error del servidor: {response.StatusCode}" : error);
            }
            return null;
        }
        catch (Exception)
        {
            // API failure, try local
            try
            {
                var store = await GetLocalStoreAsync();
                return await store.CreateUserAsync(request.Username, request.Password, request.FullName, request.Email, request.RoleName);
            }
            catch (Exception localEx)
            {
                // Both failed
                throw new Exception($"Error en el registro local: {localEx.Message}");
            }
        }
    }

    public async Task PurgeUsersAsync()
    {
        var response = await _httpClient.DeleteAsync("auth/purge-users");
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("auth/change-password", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResetPasswordAsync(Guid userId, ResetPasswordRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"auth/reset-password/{userId}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UnlockAccountAsync(Guid userId)
    {
        var response = await _httpClient.PostAsync($"auth/unlock/{userId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> NeedsBootstrapAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<bool>("auth/needs-bootstrap");
        }
        catch { return false; }
    }

    public async Task<bool> BootstrapAsync(RegisterRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("auth/bootstrap", request);
        return response.IsSuccessStatusCode;
    }

    public void Logout()
    {
        CurrentToken = null;
        CurrentUsername = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}
