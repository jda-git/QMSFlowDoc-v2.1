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
    Task<bool> UpdateTrainingAsync(UpdateTrainingRequest request);
    Task<IEnumerable<GlobalTrainingDto>> GetAllTrainingsAsync(string? staffName = null, string? competencyName = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<CompetencyEvaluation?> AssessCompetencyAsync(AssessCompetencyRequest request);
    Task<string?> GetDocumentBasePathAsync();
}

public class StaffService : IStaffService
{
    private readonly HttpClient _httpClient;
    private LocalDocumentStore? _localStore;
    private readonly NetworkConfigStore _networkConfig;

    public StaffService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
    {
        _httpClient = httpClient;
        _localStore = localStore;
        _networkConfig = networkConfig ?? new NetworkConfigStore();
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

    public async Task<IEnumerable<StaffListDto>> GetStaffAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<StaffListDto>>("staff")
                   ?? new List<StaffListDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetStaffProfilesAsync();
        }
    }

    public async Task<StaffProfile?> GetStaffProfileByIdAsync(Guid id)
    {
        try { return await _httpClient.GetFromJsonAsync<StaffProfile>($"staff/{id}"); }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetStaffProfileByIdAsync(id);
        }
    }

    public async Task<StaffProfileDetailDto?> GetStaffDetailsAsync(Guid id)
    {
        try { return await _httpClient.GetFromJsonAsync<StaffProfileDetailDto>($"staff/{id}/details"); }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetStaffProfileDetailsAsync(id);
        }
    }
    public async Task<StaffProfile?> CreateStaffProfileAsync(CreateStaffProfileRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("staff", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StaffProfile>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var profile = new StaffProfile
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PositionTitle = request.PositionTitle,
                Department = request.Department,
                HiredAt = request.HiredAt,
                IsActive = true
            };
            await store.CreateStaffProfileAsync(profile);
            return profile;
        }
    }

    public async Task<StaffProfile?> UpdateStaffProfileAsync(UpdateStaffProfileRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"staff/{request.Id}", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StaffProfile>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var profile = new StaffProfile
            {
                Id = request.Id,
                PositionTitle = request.PositionTitle,
                Department = request.Department,
                HiredAt = request.HiredAt,
                IsActive = request.IsActive
            };
            await store.UpdateStaffProfileAsync(profile);
            return profile;
        }
    }

    public async Task<bool> DeleteStaffProfileAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"staff/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteStaffProfileAsync(id);
        }
    }

    public async Task<string?> GetDocumentBasePathAsync()
    {
        var store = await GetLocalStoreAsync();
        return await store.GetBaseDocumentPathAsync();
    }

    public async Task<bool> RegisterTrainingAsync(RegisterTrainingRequest request)
    {
        // Handle File Copy locally
        string? finalRelativePath = null;
        if (!string.IsNullOrEmpty(request.SourceCertificatePath))
        {
            try
            {
                var store = await GetLocalStoreAsync();
                var basePath = await store.GetBaseDocumentPathAsync();
                if (basePath != null)
                {
                    var destDir = System.IO.Path.Combine(basePath, "personal", request.StaffId.ToString(), "certificados");
                    System.IO.Directory.CreateDirectory(destDir);
                    var fileName = System.IO.Path.GetFileName(request.SourceCertificatePath);
                    var fullDest = System.IO.Path.Combine(destDir, fileName);
                    System.IO.File.Copy(request.SourceCertificatePath, fullDest, true);
                    
                    // Store relative path using forward slashes for cross-platform compatibility if needed
                    finalRelativePath = $"personal/{request.StaffId}/certificados/{fileName}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying certificate: {ex.Message}");
                // Proceed without file? Or fail? 
                // Proceeding allows data entry even if file fails.
            }
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync("staff/training", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.RegisterTrainingAsync(request, finalRelativePath);
            return true;
        }
    }

    public async Task<bool> UpdateTrainingAsync(UpdateTrainingRequest request)
    {
         string? finalRelativePath = null;
        if (!string.IsNullOrEmpty(request.SourceCertificatePath))
        {
            try
            {
                var store = await GetLocalStoreAsync();
                var basePath = await store.GetBaseDocumentPathAsync();
                if (basePath != null)
                {
                    var destDir = System.IO.Path.Combine(basePath, "personal", request.StaffId.ToString(), "certificados");
                    System.IO.Directory.CreateDirectory(destDir);
                    var fileName = System.IO.Path.GetFileName(request.SourceCertificatePath);
                    var fullDest = System.IO.Path.Combine(destDir, fileName);
                    System.IO.File.Copy(request.SourceCertificatePath, fullDest, true);
                    
                    finalRelativePath = $"personal/{request.StaffId}/certificados/{fileName}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying certificate: {ex.Message}");
            }
        }

        try
        {
            var response = await _httpClient.PutAsJsonAsync($"staff/training/{request.Id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.UpdateTrainingAsync(request, finalRelativePath);
        }
    }

    public async Task<IEnumerable<GlobalTrainingDto>> GetAllTrainingsAsync(string? staffName = null, string? competencyName = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        // Local-first: use local store directly (API integration reserved for future)
        var store = await GetLocalStoreAsync();
        return await store.GetAllTrainingsAsync(staffName, competencyName, fromDate, toDate);
    }

    public async Task<bool> DeleteTrainingAsync(Guid trainingId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"staff/training/{trainingId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteTrainingAsync(trainingId);
        }
    }

    public async Task<CompetencyEvaluation?> AssessCompetencyAsync(AssessCompetencyRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("staff/competency", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CompetencyEvaluation>() : null;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            await store.AssessCompetencyAsync(request);
            // Construct basic return object
            return new CompetencyEvaluation
            {
                Id = Guid.NewGuid(),
                StaffId = request.StaffId,
                EvaluationDate = request.EvaluationDate,
                Outcome = request.Outcome.ToString()
            };
        }
    }
}
