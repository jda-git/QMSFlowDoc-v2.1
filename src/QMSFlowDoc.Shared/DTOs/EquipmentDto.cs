using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class EquipmentListDto
{
    public Guid Id { get; set; }
    public string? AssetTag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? SoftwareVersion { get; set; } // ISP 15189
    public string? FirmwareVersion { get; set; } // ISP 15189
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; }
    public Guid? LastMaintenanceEventId { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
    public string? LastEventType { get; set; }
    public string? LastOutcome { get; set; }
    public string? NextMaintenanceDue { get; set; }

    public EquipmentListDto() { }
    public EquipmentListDto(Guid id, string? tag, string name, string? model, string? sw, string? fw, string? loc, EquipmentStatus status, Guid? lastId, DateTime? last, string? eventType, string? outcome, string? next)
    {
        Id = id; AssetTag = tag; Name = name; Model = model; SoftwareVersion = sw; FirmwareVersion = fw; Location = loc; Status = status;
        LastMaintenanceEventId = lastId; LastMaintenanceAt = last; LastEventType = eventType; LastOutcome = outcome; NextMaintenanceDue = next;
    }

    // Helper properties for UI binding
    public string LastMaintenanceDateFormatted => LastMaintenanceAt?.ToString("dd/MM/yyyy") ?? "-";
    public string StatusFormatted => Status.ToString();
}

public record CreateEquipmentRequest(
    string? AssetTag,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? SoftwareVersion,
    string? FirmwareVersion,
    string? Location,
    DateTime? InstalledAt
);

public record UpdateEquipmentRequest(
    Guid Id,
    string? AssetTag,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? SoftwareVersion,
    string? FirmwareVersion,
    string? Location,
    DateTime? InstalledAt
);

public record RegisterMaintenanceRequest(
    Guid EquipmentId,
    Guid? PlanId,
    DateTime? PerformedAt,
    MaintenanceEventType EventType,
    string? Outcome,
    string? Notes,
    Guid? EvidenceDocId,
    bool? HasIssues,
    int? NextMaintenanceMonth,
    int? NextMaintenanceYear
);

public record UpdateMaintenanceRequest(
    Guid Id,
    DateTime? PerformedAt,
    MaintenanceEventType EventType,
    string? Outcome,
    string? Notes,
    bool? HasIssues,
    int? NextMaintenanceMonth,
    int? NextMaintenanceYear
);
