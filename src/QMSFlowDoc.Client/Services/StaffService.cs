using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IStaffService
{
    Task<IEnumerable<StaffListDto>> GetStaffAsync();
    Task<StaffProfile?> GetStaffProfileByIdAsync(Guid id);
    Task<StaffProfileDetailDto?> GetStaffDetailsAsync(Guid id);
    Task<StaffProfile?> CreateStaffProfileAsync(CreateStaffProfileRequest request);
    Task<StaffProfile?> UpdateStaffProfileAsync(UpdateStaffProfileRequest request);
    Task<bool> DeleteStaffProfileAsync(Guid id);
    Task<bool> RegisterTrainingAsync(RegisterTrainingRequest request);
    Task<bool> DeleteTrainingAsync(Guid trainingId);
    Task<CompetencyEvaluation?> AssessCompetencyAsync(AssessCompetencyRequest request);
}

public class StaffService : IStaffService
{
    private readonly HttpClient _httpClient;

    public StaffService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<StaffListDto>> GetStaffAsync()
    {
        return await _httpClient.GetFromJsonAsync<IEnumerable<StaffListDto>>("staff")
               ?? new List<StaffListDto>();
    }

    public async Task<StaffProfile?> GetStaffProfileByIdAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<StaffProfile>($"staff/{id}");
    }

    public async Task<StaffProfileDetailDto?> GetStaffDetailsAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<StaffProfileDetailDto>($"staff/{id}/details");
    }

    public async Task<StaffProfile?> CreateStaffProfileAsync(CreateStaffProfileRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("staff", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StaffProfile>();
        }
        
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception(string.IsNullOrWhiteSpace(error) ? $"Error del servidor: {response.StatusCode}" : error);
    }

    public async Task<StaffProfile?> UpdateStaffProfileAsync(UpdateStaffProfileRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"staff/{request.Id}", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StaffProfile>();
        }

        var error = await response.Content.ReadAsStringAsync();
        throw new Exception(string.IsNullOrWhiteSpace(error) ? $"Error del servidor: {response.StatusCode}" : error);
    }

    public async Task<bool> DeleteStaffProfileAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"staff/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RegisterTrainingAsync(RegisterTrainingRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("staff/training", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteTrainingAsync(Guid trainingId)
    {
        var response = await _httpClient.DeleteAsync($"staff/training/{trainingId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<CompetencyEvaluation?> AssessCompetencyAsync(AssessCompetencyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("staff/assess", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CompetencyEvaluation>() : null;
    }
}
