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

    public AuditListDto() { }
    public AuditListDto(Guid id, string title, DateTime date, AuditStatus status, int findings)
    {
        Id = id; Title = title; ScheduledDate = date; Status = status; FindingCount = findings;
    }
}

public record CreateAuditRequest(
    string Title,
    DateTime ScheduledDate,
    string Scope,
    string? LeadAuditor,
    Guid? ReportDocumentId = null
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

    public ManagementReviewListDto() { }
    public ManagementReviewListDto(Guid id, DateTime date, string summary)
    {
        Id = id; ReviewDate = date; Summary = summary;
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

