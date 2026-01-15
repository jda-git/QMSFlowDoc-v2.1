using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public enum EQAStatus
{
    ACTIVE,
    INACTIVE,
    ARCHIVED
}

public enum EQAResultStatus
{
    PENDING,
    SUBMITTED,
    EVALUATED
}

public enum EQAPerformance
{
    SATISFACTORY,
    UNSATISFACTORY,
    WARNING,
    NOT_EVALUATED
}

public class EQAProgram
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? CycleFrequency { get; set; } // Monthly, Quarterly
    public EQAStatus Status { get; set; } = EQAStatus.ACTIVE;
    public string? Notes { get; set; }
    
    public List<EQAResult> Results { get; set; } = new();
}

public class EQAResult
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public string CycleIdentifier { get; set; } = string.Empty; // e.g. "2024-01"
    public DateTime? ReceiptDate { get; set; }
    public DateTime? ProcessingDate { get; set; }
    public DateTime? SubmissionDate { get; set; }
    public EQAResultStatus Status { get; set; } = EQAResultStatus.PENDING;
    
    // Evaluation
    public decimal? Score { get; set; }
    public EQAPerformance Performance { get; set; } = EQAPerformance.NOT_EVALUATED;
    public string? Notes { get; set; }
    public Guid? EvidenceDocId { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public DateTime? ReviewDate { get; set; }
}
