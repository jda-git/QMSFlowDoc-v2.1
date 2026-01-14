using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public enum EquipmentStatus
{
    ACTIVE,
    OUT_OF_SERVICE,
    RETIRED
}

public enum MaintenanceEventType
{
    PREVENTIVE,
    CORRECTIVE,
    INSPECTION,
    CALIBRATION
}

public class Equipment
{
    public Guid Id { get; set; }
    public string? AssetTag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SoftwareVersion { get; set; } // ISO 15189 Req 3.1
    public string? FirmwareVersion { get; set; } // ISO 15189 Req 3.1
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.ACTIVE;
    public DateTime? InstalledAt { get; set; }
    public string? Notes { get; set; }

    public List<MaintenancePlan> MaintenancePlans { get; set; } = new();
    public List<MaintenanceEvent> MaintenanceEvents { get; set; } = new();
}

public class MaintenancePlan
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int FrequencyDays { get; set; }
    public string ChecklistJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
}

public class MaintenanceEvent
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid? PlanId { get; set; }
    public DateTime PerformedAt { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public MaintenanceEventType EventType { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public Guid? EvidenceDocId { get; set; }
    public bool? HasIssues { get; set; }
    public int? NextMaintenanceMonth { get; set; }
    public int? NextMaintenanceYear { get; set; }
}
