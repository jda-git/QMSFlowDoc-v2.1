using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Application.Services.Quality;

public interface IQualityService
{
    // Non-Conformities
    Task<List<NCListDto>> GetNonconformitiesAsync();
    Task<Nonconformity?> GetNCByIdAsync(Guid id);
    Task<Guid> CreateNCAsync(CreateNCRequest request);
    Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request);
    Task<bool> UpdateNCStatusAsync(Guid id, NCStatus status, Guid? userId = null);
    Task<bool> DeleteNCAsync(Guid id);

    // CAPA Actions
    Task<Guid> CreateCAPAAsync(CreateCAPARequest request);
    Task<bool> UpdateCAPAStatusAsync(Guid id, CAPAStatus status);
    Task<bool> CompleteCAPAAsync(Guid id, string? effectivenessCheck, Guid? userId = null);
    Task<bool> DeleteCAPAAsync(Guid id);

    // Complaints
    Task<List<ComplaintListDto>> GetComplaintsAsync();
    Task<Complaint?> GetComplaintByIdAsync(Guid id);
    Task<Guid> CreateComplaintAsync(CreateComplaintRequest request);
    Task<bool> UpdateComplaintAsync(Guid id, CreateComplaintRequest request);
    Task<bool> UpdateComplaintStatusAsync(Guid id, ComplaintStatus status, Guid? userId = null);
    Task<bool> DeleteComplaintAsync(Guid id);
}
