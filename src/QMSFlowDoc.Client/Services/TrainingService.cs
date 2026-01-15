using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface ITrainingService
{
    Task<IEnumerable<TrainingActivityDto>> GetActivitiesAsync();
    Task<TrainingActivity?> CreateActivityAsync(CreateTrainingActivityRequest request);
}

public class TrainingService : ITrainingService
{
    private readonly HttpClient _httpClient;
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public TrainingService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
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

    public async Task<IEnumerable<TrainingActivityDto>> GetActivitiesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<TrainingActivityDto>>("training")
                   ?? new List<TrainingActivityDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetTrainingActivitiesAsync();
        }
    }

    public async Task<TrainingActivity?> CreateActivityAsync(CreateTrainingActivityRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("training", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TrainingActivity>();
            }
            throw new HttpRequestException($"Error API ({response.StatusCode})");
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var activity = new TrainingActivity
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Provider = request.Provider,
                TrainingTypeId = request.TrainingTypeId ?? Guid.Empty,
                Modality = request.Modality ?? "PRESENCIAL",
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Hours = request.Hours,
                Description = request.Description,
                IsInternal = request.IsInternal,
                CreatedByUserId = Guid.Empty, // Placeholder
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            return await store.CreateTrainingActivityAsync(activity);
        }
    }
}
