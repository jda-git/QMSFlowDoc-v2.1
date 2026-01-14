using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class ReagentListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string ReagentType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public ReagentStatus Status { get; set; }
    public decimal TotalStock { get; set; }
    public decimal MinStock { get; set; }
    public decimal TargetStock { get; set; }
    public string? Fluorescence { get; set; }
    public string? InternalCode { get; set; }
    public List<LotSummaryDto> AvailableLots { get; set; } = new();

    public string LotFechaSummary => string.Join(" ", AvailableLots.Select(l => l.FormattedString));

    public ReagentListDto() { }
    public ReagentListDto(Guid id, string name, string? manu, string? code, string? fluorescence, string type, string reference, ReagentStatus status, decimal stock, decimal min, decimal target)
    {
        Id = id; Name = name; Manufacturer = manu; InternalCode = code; Fluorescence = fluorescence;
        ReagentType = type; Reference = reference; Status = status; TotalStock = stock; MinStock = min; TargetStock = target;
    }

    public override string ToString()
    {
        var parts = new List<string> { Name };
        if (!string.IsNullOrWhiteSpace(Fluorescence)) parts.Add($"[{Fluorescence}]");
        if (!string.IsNullOrWhiteSpace(InternalCode)) parts.Add($"Cod:{InternalCode}");
        if (!string.IsNullOrWhiteSpace(Manufacturer)) parts.Add($"Fab:{Manufacturer}");
        return string.Join(" - ", parts);
    }
}

public class LotSummaryDto
{
    public Guid Id { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string FormattedString => $"LOTE:{LotNumber} FECHA:{ExpiryDate:MM/yy}";
}

public record CreateReagentRequest(
    string Name,
    string? Manufacturer,
    Guid? SupplierId,
    string? ManufacturerCode,
    string? InternalCode,
    string? Fluorescence,
    string ReagentType,
    string Reference,
    string? Classification, // ISO 15189
    string? StorageConditions,
    Guid? DefaultLocationId,
    int? OpenShelfLifeDays,
    decimal MinStock,
    decimal TargetStock,
    decimal ReorderQty
);

public record RegisterLotRequest(
    Guid ReagentId,
    string LotNumber,
    DateTime ExpiryDate,
    DateTime ReceivedDate,
    decimal ReceivedQty,
    Guid? LocationId,
    Guid? PanelId // ISO 15189
);

public record AdjustStockRequest(
    Guid ReagentId,
    Guid? ReagentLotId,
    InventoryMovementType MovementType,
    decimal Qty,
    string Reason,
    string? Notes = null
);


public record InventoryMovementDto(
    Guid Id,
    DateTime MovedAt,
    string UserName,
    string ReagentName,
    string? Manufacturer,
    string? Fluorescence,
    string AdjustmentType, // IN, OUT, etc.
    decimal Qty,
    string? LotNumber,
    DateTime? ExpiryDate,
    string Reason
);
