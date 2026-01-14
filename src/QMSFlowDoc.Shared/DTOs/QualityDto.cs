using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class NCListDto
{
    public Guid Id { get; set; }
    public DateTime DetectedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public NCSeverity Severity { get; set; }
    public NCStatus Status { get; set; }
    public bool ImpactPatient { get; set; }
    public int ActionCount { get; set; }

    public string? Origin { get; set; } // ISO 15189
    public string? RootCauseAnalysis { get; set; } // ISO 15189

    public NCListDto() { }
    public NCListDto(Guid id, DateTime det, string title, NCSeverity sev, NCStatus stat, bool impact, int actions, string? origin, string? rca)
    {
        Id = id; DetectedAt = det; Title = title; Severity = sev; Status = stat; ImpactPatient = impact; ActionCount = actions;
        Origin = origin; RootCauseAnalysis = rca;
    }
}

public record CreateNCRequest(
    string Title,
    string Description,
    NCSeverity Severity,
    NCStatus? Status,
    bool ImpactPatient,
    string? Containment,
    string? Origin,
    string? RootCauseAnalysis
);

public record CreateCAPARequest(
    Guid NCId,
    CAPAActionType ActionType,
    string Description,
    Guid? OwnerUserId,
    DateTime? DueDate
);

