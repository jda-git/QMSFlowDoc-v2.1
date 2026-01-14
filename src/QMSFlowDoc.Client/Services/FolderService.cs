using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using System.Linq; // Added for Linq extension methods

namespace QMSFlowDoc.Client.Services;

public interface IFolderService
{
    Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid? parentId = null);
    Task<bool> CreateFolderAsync(string name, Guid? parentId = null);
    Task<bool> RenameFolderAsync(Guid id, string newName);
    Task<bool> DeleteFolderAsync(Guid id);
}

public class FolderService : IFolderService
{
    private readonly HttpClient _httpClient;
    private readonly NetworkConfigStore? _networkConfig; // Made nullable
    private readonly LocalDocumentStore? _localStore;
    private bool _useLocalMode = false;

    // Use constructor injection for LocalDocumentStore if available, otherwise creaate locally (or handle as shared)
    // To minimize App.xaml.cs refactoring risk right now, we will inspect the environment or re-use the store logic.
    // However, best practice is to pass it. Let's assume we will update App.xaml.cs to pass it.
    public FolderService(HttpClient httpClient, LocalDocumentStore? localStore = null)
    {
        _httpClient = httpClient;
        _localStore = localStore;
        
         // Simple check: if localStore provided, we might use it.
         // Real check: relies on HttpClient health or config. 
         // For now, let's sync with DocumentService logic: try health check?
         // Duplicating logic is bad. Let's assume App passes the "mode" or store.
         // If _localStore is not null, checking health might be redundant if we want to enforce it.
         // Let's copy the specific check from DocumentService for consistency/safety.
         
        try
        {
            var testTask = _httpClient.GetAsync("health");
            testTask.Wait(TimeSpan.FromMilliseconds(500));
            _useLocalMode = !testTask.Result.IsSuccessStatusCode;
        }
        catch
        {
            _useLocalMode = true;
        }

        if (_useLocalMode && _localStore == null)
        {
            // Fallback: create internal store if not provided (emergency, though App.xaml.cs should provide it)
            // But we can't create it easily without config. 
            // Better to rely on App.xaml.cs update.
            _networkConfig = new NetworkConfigStore();
            _localStore = new LocalDocumentStore(_networkConfig);
        }
    }

    public async Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid? parentId = null)
    {
        if (_useLocalMode && _localStore != null)
        {
            var allFolders = await _localStore.GetFoldersAsync();
            // Filter by parentId in memory since Sqlite query gets all (for now)
            return allFolders.Where(f => f.ParentFolderId == parentId).ToList();
        }
        
        var url = "folders";
        if (parentId.HasValue) url += $"?parentId={parentId.Value}";
        
        try {
            return await _httpClient.GetFromJsonAsync<IEnumerable<FolderDto>>(url) ?? new List<FolderDto>();
        } catch { return new List<FolderDto>(); }
    }

    public async Task<bool> CreateFolderAsync(string name, Guid? parentId = null)
    {
        if (_useLocalMode && _localStore != null)
        {
            return await _localStore.CreateFolderAsync(name, parentId);
        }

        var response = await _httpClient.PostAsync($"folders?name={Uri.EscapeDataString(name)}&parentId={parentId}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RenameFolderAsync(Guid id, string newName)
    {
        if (_useLocalMode && _localStore != null)
        {
            return await _localStore.RenameFolderAsync(id, newName);
        }

        var response = await _httpClient.PutAsync($"folders/{id}?name={Uri.EscapeDataString(newName)}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteFolderAsync(Guid id)
    {
        if (_useLocalMode && _localStore != null)
        {
            return await _localStore.DeleteFolderAsync(id);
        }

        var response = await _httpClient.DeleteAsync($"folders/{id}");
        return response.IsSuccessStatusCode;
    }
}
