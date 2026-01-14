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
    Task<bool> NeedsBootstrapAsync();
    Task<bool> BootstrapAsync(RegisterRequest request);
    void Logout();
    string? CurrentToken { get; }
    string? CurrentUsername { get; }
    List<string> CurrentRoles { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    public string? CurrentToken { get; private set; }
    public string? CurrentUsername { get; private set; }
    public List<string> CurrentRoles { get; private set; } = new();
    public bool IsAuthenticated => !string.IsNullOrEmpty(CurrentToken);
    public bool IsAdmin => CurrentRoles.Contains("Administrador");

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
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
                    CurrentRoles = result.Roles ?? new List<string>();
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CurrentToken);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
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
        catch (Exception ex)
        {
            throw new Exception($"Error en el registro: {ex.Message}");
        }
    }

    public async Task PurgeUsersAsync()
    {
        var response = await _httpClient.DeleteAsync("auth/purge-users");
        response.EnsureSuccessStatusCode();
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
