using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class RiskListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Score { get; set; }
    public RiskStatus Status { get; set; }

    public RiskLikelihood Likelihood { get; set; }
    public RiskImpact Impact { get; set; }

    public RiskListDto() { }
    public RiskListDto(Guid id, string title, string cat, int score, RiskStatus status, RiskLikelihood like, RiskImpact imp)
    {
        Id = id; Title = title; Category = cat; Score = score; Status = status; Likelihood = like; Impact = imp;
    }
}

public record CreateRiskRequest(
    string Title,
    string Description,
    string Category,
    RiskLikelihood Likelihood,
    RiskImpact Impact,
    string? MitigationPlan,
    Guid? OwnerUserId
);

public class AuditListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public AuditStatus Status { get; set; }
    public int FindingCount { get; set; }

    public Guid? ReportDocumentId { get; set; }

    public AuditListDto() { }
    public AuditListDto(Guid id, string title, DateTime date, AuditStatus status, int findings, Guid? reportDocId)
    {
        Id = id; Title = title; ScheduledDate = date; Status = status; FindingCount = findings; ReportDocumentId = reportDocId;
    }
}

public record CreateAuditRequest(
    string Title,
    DateTime ScheduledDate,
    string Scope,
    string? LeadAuditor,
    Guid? ReportDocumentId = null,
    string? ChecklistJson = null
);

public record RegisterFindingRequest(
    Guid AuditPlanId,
    string Description,
    string? IsoRequirement,
    FindingType Type,
    Guid? RelatedNCId = null
);

public class ManagementReviewListDto
{
    public Guid Id { get; set; }
    public DateTime ReviewDate { get; set; }
    public string Summary { get; set; } = string.Empty;

    public Guid? MinutesDocumentId { get; set; }

    public ManagementReviewListDto() { }
    public ManagementReviewListDto(Guid id, DateTime date, string summary, Guid? minutesDocId)
    {
        Id = id; ReviewDate = date; Summary = summary; MinutesDocumentId = minutesDocId;
    }
}

public record CreateManagementReviewRequest(
    DateTime ReviewDate,
    string Participants,
    string Agenda,
    string Summary,
    string? Actions,
    Guid? MinutesDocumentId = null
);

// === IQC DTOs ===

public class IQCListDto
{
    public Guid Id { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string AnalyteName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public double Value { get; set; }
    public IQCStatus Status { get; set; }
    public DateTime Date { get; set; }
    public string? WestgardRule { get; set; }

    public IQCListDto() { }
}

public record CreateIQCResultRequest(
    string EquipmentName,
    string AnalyteName,
    string Level,
    double Value,
    double Mean,
    double SD,
    DateTime Date,
    string? Comments
);

// === Contingency DTOs ===

public class ContingencyListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public ContingencyStatus Status { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public string LastReviewDateDisplay => LastReviewDate.HasValue ? LastReviewDate.Value.ToString("d") : "Pendiente";

    public ContingencyListDto() { }
}

public record CreateContingencyPlanRequest(
    string Title,
    string TriggerEvent,
    string ProcedureSteps,
    string? ResponsiblePerson
);
