using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public enum RiskLikelihood
{
    RARE = 1,
    UNLIKELY = 2,
    POSSIBLE = 3,
    LIKELY = 4,
    ALMOST_CERTAIN = 5
}

public enum RiskImpact
{
    INSIGNIFICANT = 1,
    MINOR = 2,
    MODERATE = 3,
    MAJOR = 4,
    CATASTROPHIC = 5
}

public enum RiskStatus
{
    ACTIVE,
    MITIGATED,
    ACCEPTED,
    RETIRED
}

public enum AuditStatus
{
    PLANNED,
    IN_PROGRESS,
    COMPLETED,
    CANCELLED
}

public enum FindingType
{
    OBSERVATION,
    MINOR_NC,
    MAJOR_NC,
    OPPORTUNITY_FOR_IMPROVEMENT
}

public class Risk
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "OPERATIONAL";
    public RiskLikelihood Likelihood { get; set; }
    public RiskImpact Impact { get; set; }
    public int RiskScore => (int)Likelihood * (int)Impact;
    public string? MitigationPlan { get; set; }
    public Guid? OwnerUserId { get; set; }
    public User? Owner { get; set; }
    public RiskStatus Status { get; set; } = RiskStatus.ACTIVE;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AuditPlan
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string? LeadAuditor { get; set; }
    public AuditStatus Status { get; set; } = AuditStatus.PLANNED;
    public string? SummaryReport { get; set; }
    
    public Guid? ReportDocumentId { get; set; }
    public Document? ReportDocument { get; set; }

    public List<AuditFinding> Findings { get; set; } = new();
}

public class AuditFinding
{
    public Guid Id { get; set; }
    public Guid AuditPlanId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IsoRequirement { get; set; }
    public FindingType Type { get; set; }
    public Guid? RelatedNCId { get; set; } // Link to Nonconformity if it becomes one
    public Nonconformity? RelatedNC { get; set; }
}

public class ManagementReview
{
    public Guid Id { get; set; }
    public DateTime ReviewDate { get; set; }
    public string Participants { get; set; } = string.Empty;
    public string Agenda { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Actions { get; set; }
    
    public Guid? MinutesDocumentId { get; set; }
    public Document? MinutesDocument { get; set; }
}
