using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Quality;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Shared.DTOs;
using DomainEntities = QMSFlowDoc.Domain.Entities;
using SharedModels = QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Infrastructure.Services.Quality;

public class QualityService : IQualityService
{
    private readonly QmsDbContext _context;

    public QualityService(QmsDbContext context)
    {
        _context = context;
    }

    // ── Non-Conformities ─────────────────────────────────────────────

    public async Task<List<NCListDto>> GetNonconformitiesAsync()
    {
        return await _context.Nonconformities
            .Include(n => n.Actions)
            .OrderByDescending(n => n.DetectedAt)
            .Select(n => new NCListDto
            {
                Id = n.Id,
                DetectedAt = n.DetectedAt,
                Title = n.Title,
                Severity = (SharedModels.NCSeverity)n.Severity,
                Status = (SharedModels.NCStatus)n.Status,
                ImpactPatient = n.ImpactPatient,
                ActionCount = n.Actions.Count,
                Origin = n.Origin,
                RootCauseAnalysis = n.RootCauseAnalysis
            })
            .ToListAsync();
    }

    public async Task<SharedModels.Nonconformity?> GetNCByIdAsync(Guid id)
    {
        var nc = await _context.Nonconformities
            .Include(n => n.Actions)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (nc == null) return null;

        // Fetch user names for closure and actions
        string? closedByName = null;
        if (nc.ClosedByUserId.HasValue)
        {
            closedByName = (await _context.Users.FindAsync(nc.ClosedByUserId.Value))?.FullName;
        }

        // Fetch user names for actions
        var actionDtos = new List<SharedModels.CapaAction>();
        foreach (var a in nc.Actions)
        {
            string? actionClosedByName = null;
            if (a.ClosedByUserId.HasValue)
            {
                actionClosedByName = (await _context.Users.FindAsync(a.ClosedByUserId.Value))?.FullName;
            }
            actionDtos.Add(new SharedModels.CapaAction
            {
                Id = a.Id,
                NCId = a.NCId,
                ActionType = (SharedModels.CAPAActionType)a.ActionType,
                Description = a.Description,
                OwnerUserId = a.OwnerUserId,
                ClosedByUserId = a.ClosedByUserId,
                ClosedByUserName = actionClosedByName,
                DueDate = a.DueDate,
                CompletedAt = a.CompletedAt,
                EffectivenessCheck = a.EffectivenessCheck,
                Status = (SharedModels.CAPAStatus)a.Status
            });
        }

        return new SharedModels.Nonconformity
        {
            Id = nc.Id,
            DetectedAt = nc.DetectedAt,
            DetectedByUserId = nc.DetectedByUserId,
            Title = nc.Title,
            Description = nc.Description,
            Severity = (SharedModels.NCSeverity)nc.Severity,
            ImpactPatient = nc.ImpactPatient,
            Containment = nc.Containment,
            Origin = nc.Origin,
            RootCauseAnalysis = nc.RootCauseAnalysis,
            Status = (SharedModels.NCStatus)nc.Status,
            UpdatedAt = nc.UpdatedAt,
            ClosedAt = nc.ClosedAt,
            ClosedByUserId = nc.ClosedByUserId,
            ClosedByUserName = closedByName,
            RowVersion = nc.RowVersion,
            Actions = actionDtos
        };
    }

    public async Task<Guid> CreateNCAsync(CreateNCRequest request)
    {
        var nc = new DomainEntities.Nonconformity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Severity = (DomainEntities.NCSeverity)request.Severity,
            Status = request.Status.HasValue ? (DomainEntities.NCStatus)request.Status.Value : DomainEntities.NCStatus.OPEN,
            ImpactPatient = request.ImpactPatient,
            Containment = request.Containment,
            Origin = request.Origin,
            RootCauseAnalysis = request.RootCauseAnalysis,
            DetectedByUserId = request.DetectedByUserId,
            DetectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Nonconformities.Add(nc);
        await _context.SaveChangesAsync();
        return nc.Id;
    }

    public async Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request)
    {
        var nc = await _context.Nonconformities.FindAsync(id);
        if (nc == null) return false;

        nc.Title = request.Title;
        nc.Description = request.Description;
        nc.Severity = (DomainEntities.NCSeverity)request.Severity;
        nc.ImpactPatient = request.ImpactPatient;
        nc.Containment = request.Containment;
        nc.Origin = request.Origin;
        nc.RootCauseAnalysis = request.RootCauseAnalysis;
        nc.DetectedByUserId = request.DetectedByUserId;
        if (request.Status.HasValue)
            nc.Status = (DomainEntities.NCStatus)request.Status.Value;
        nc.UpdatedAt = DateTime.UtcNow;

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateNCStatusAsync(Guid id, SharedModels.NCStatus status, Guid? userId = null)
    {
        var nc = await _context.Nonconformities.FindAsync(id);
        if (nc == null) return false;

        nc.Status = (DomainEntities.NCStatus)status;
        nc.UpdatedAt = DateTime.UtcNow;

        if (status == SharedModels.NCStatus.CLOSED)
        {
            nc.ClosedAt = DateTime.UtcNow;
            nc.ClosedByUserId = userId;
        }
        else
        {
            nc.ClosedAt = null;
            nc.ClosedByUserId = null;
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteNCAsync(Guid id)
    {
        var nc = await _context.Nonconformities.FindAsync(id);
        if (nc == null) return false;

        _context.Nonconformities.Remove(nc);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── CAPA Actions ─────────────────────────────────────────────────

    public async Task<Guid> CreateCAPAAsync(CreateCAPARequest request)
    {
        var capa = new DomainEntities.CapaAction
        {
            Id = Guid.NewGuid(),
            NCId = request.NCId,
            ActionType = (DomainEntities.CAPAActionType)request.ActionType,
            Description = request.Description,
            OwnerUserId = request.OwnerUserId,
            DueDate = request.DueDate,
            Status = DomainEntities.CAPAStatus.OPEN
        };

        _context.CapaActions.Add(capa);
        await _context.SaveChangesAsync();
        return capa.Id;
    }

    public async Task<bool> UpdateCAPAStatusAsync(Guid id, SharedModels.CAPAStatus status)
    {
        var capa = await _context.CapaActions.FindAsync(id);
        if (capa == null) return false;

        capa.Status = (DomainEntities.CAPAStatus)status;
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> CompleteCAPAAsync(Guid id, string? effectivenessCheck, Guid? userId = null)
    {
        var capa = await _context.CapaActions.FindAsync(id);
        if (capa == null) return false;

        capa.Status = DomainEntities.CAPAStatus.DONE;
        capa.CompletedAt = DateTime.UtcNow;
        capa.EffectivenessCheck = effectivenessCheck;
        capa.ClosedByUserId = userId;

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteCAPAAsync(Guid id)
    {
        var capa = await _context.CapaActions.FindAsync(id);
        if (capa == null) return false;

        _context.CapaActions.Remove(capa);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Complaints ───────────────────────────────────────────────────

    public async Task<List<ComplaintListDto>> GetComplaintsAsync()
    {
        return await _context.Complaints
            .OrderByDescending(c => c.Date)
            .Select(c => new ComplaintListDto
            {
                Id = c.Id,
                Date = c.Date,
                Source = c.Source,
                Description = c.Description,
                Category = (SharedModels.ComplaintCategory)c.Category,
                Status = (SharedModels.ComplaintStatus)c.Status
            })
            .ToListAsync();
    }

    public async Task<SharedModels.Complaint?> GetComplaintByIdAsync(Guid id)
    {
        var c = await _context.Complaints
            .Include(x => x.Actions)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (c == null) return null;

        string? closedByName = null;
        if (c.ClosedByUserId.HasValue)
        {
            closedByName = (await _context.Users.FindAsync(c.ClosedByUserId.Value))?.FullName;
        }

        return new SharedModels.Complaint
        {
            Id = c.Id,
            Date = c.Date,
            Source = c.Source,
            Description = c.Description,
            Category = (SharedModels.ComplaintCategory)c.Category,
            ClaimantType = (SharedModels.ClaimantType)c.ClaimantType,
            IsSubstantiated = c.IsSubstantiated,
            ReceiptDate = c.ReceiptDate,
            ReceiptMethod = c.ReceiptMethod,
            ClinicalImpact = (SharedModels.ClinicalImpact)c.ClinicalImpact,
            RelatedNCId = c.RelatedNCId,
            ResolutionEvidence = c.ResolutionEvidence,
            EffectivenessDate = c.EffectivenessDate,
            EffectivenessVerifiedBy = c.EffectivenessVerifiedBy,
            EffectivenessNotes = c.EffectivenessNotes,
            InvestigationResult = c.InvestigationResult,
            CorrectiveAction = c.CorrectiveAction,
            Status = (SharedModels.ComplaintStatus)c.Status,
            ClosedAt = c.ClosedAt,
            ClosedByUserId = c.ClosedByUserId,
            ClosedByUserName = closedByName,
            RowVersion = c.RowVersion,
            Actions = c.Actions.Select(a => new SharedModels.ComplaintAction
            {
                Id = a.Id,
                ComplaintId = a.ComplaintId,
                ActionType = (SharedModels.ComplaintActionType)a.ActionType,
                Description = a.Description,
                OwnerUserId = a.OwnerUserId,
                DueDate = a.DueDate,
                CompletedDate = a.CompletedDate,
                Status = (SharedModels.ActionStatus)a.Status
            }).ToList()
        };
    }

    public async Task<Guid> CreateComplaintAsync(CreateComplaintRequest request)
    {
        var complaint = new DomainEntities.Complaint
        {
            Id = Guid.NewGuid(),
            Source = request.Source,
            Description = request.Description,
            Category = (DomainEntities.ComplaintCategory)request.Category,
            InvestigationResult = request.InvestigationResult,
            CorrectiveAction = request.CorrectiveAction,
            Date = DateTime.UtcNow,
            Status = DomainEntities.ComplaintStatus.OPEN
        };

        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync();
        return complaint.Id;
    }

    public async Task<bool> UpdateComplaintAsync(Guid id, CreateComplaintRequest request)
    {
        var complaint = await _context.Complaints.FindAsync(id);
        if (complaint == null) return false;

        complaint.Source = request.Source;
        complaint.Description = request.Description;
        complaint.Category = (DomainEntities.ComplaintCategory)request.Category;
        complaint.InvestigationResult = request.InvestigationResult;
        complaint.CorrectiveAction = request.CorrectiveAction;

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateComplaintStatusAsync(Guid id, SharedModels.ComplaintStatus status, Guid? userId = null)
    {
        var complaint = await _context.Complaints.FindAsync(id);
        if (complaint == null) return false;

        complaint.Status = (DomainEntities.ComplaintStatus)status;
        if (status == SharedModels.ComplaintStatus.CLOSED)
        {
            complaint.ClosedAt = DateTime.UtcNow;
            complaint.ClosedByUserId = userId;
        }
        else
        {
            complaint.ClosedAt = null;
            complaint.ClosedByUserId = null;
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteComplaintAsync(Guid id)
    {
        var complaint = await _context.Complaints.FindAsync(id);
        if (complaint == null) return false;

        _context.Complaints.Remove(complaint);
        return await _context.SaveChangesAsync() > 0;
    }
}
