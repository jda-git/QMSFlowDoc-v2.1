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

    public TrainingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<TrainingActivityDto>> GetActivitiesAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<TrainingActivityDto>>("training")
               ?? new List<TrainingActivityDto>();
    }

    public async Task<TrainingActivity?> CreateActivityAsync(CreateTrainingActivityRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("training", request);
        return response.IsSuccessStatusCode 
            ? await response.Content.ReadFromJsonAsync<TrainingActivity>() 
            : null;
    }
}
