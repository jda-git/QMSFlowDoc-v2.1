using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public enum NCSeverity
{
    LOW,
    MEDIUM,
    HIGH,
    CRITICAL
}

public enum NCStatus
{
    OPEN,
    INVESTIGATING,
    ACTION,
    CLOSED
}

public enum CAPAActionType
{
    CORRECTIVE,
    PREVENTIVE
}

public enum CAPAStatus
{
    OPEN,
    DONE,
    VERIFIED,
    CANCELLED
}

public class Nonconformity
{
    public Guid Id { get; set; }
    public DateTime DetectedAt { get; set; }
    public Guid? DetectedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NCSeverity Severity { get; set; } = NCSeverity.LOW;
    public bool ImpactPatient { get; set; } = false;
    public string? Containment { get; set; }
    
    // ISO 15189 Req 5.1 & 5.2
    public string? Origin { get; set; } // Equipment, Reagent, Process, Document
    public string? RootCauseAnalysis { get; set; }
    
    public NCStatus Status { get; set; } = NCStatus.OPEN;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CapaAction> Actions { get; set; } = new();
}

public class CapaAction
{
    public Guid Id { get; set; }
    public Guid? NCId { get; set; }
    public Nonconformity? Nonconformity { get; set; }
    public CAPAActionType ActionType { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? EffectivenessCheck { get; set; }
    public CAPAStatus Status { get; set; } = CAPAStatus.OPEN;
}

// === Quejas y Reclamaciones (ISO 15189 §7.7) ===

public enum ComplaintStatus { OPEN, INVESTIGATING, CLOSED }
public enum ComplaintCategory { PATIENT, CLINICAL, TURNAROUND, REPORT_ERROR, OTHER }

public class Complaint
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Source { get; set; } = string.Empty; // Who filed it
    public string Description { get; set; } = string.Empty;
    public ComplaintCategory Category { get; set; } = ComplaintCategory.OTHER;
    public string? InvestigationResult { get; set; }
    public string? CorrectiveAction { get; set; }
    public ComplaintStatus Status { get; set; } = ComplaintStatus.OPEN;
    public DateTime? ClosedAt { get; set; }
}
