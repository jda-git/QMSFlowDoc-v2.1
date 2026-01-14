using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QMSFlowDoc.Shared.Models;

public enum ReagentStatus
{
    ACTIVO,
    EN_CAMBIO,
    OBSOLETO,
    BLOQUEADO
}

public enum LotStatus
{
    QUARANTINE,
    RELEASED,
    BLOCKED,
    CONSUMED,
    EXPIRED,
    RECALLED
}

public enum InventoryMovementType
{
    IN,
    OUT,
    ADJUST,
    WASTE,
    TRANSFER,
    RETURN
}

public class Supplier
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
}

public class StorageLocation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Reagent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? ManufacturerCode { get; set; }
    public string? InternalCode { get; set; }
    public string Fluorescence { get; set; } = string.Empty;
    public string ReagentType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    
    // ISO 15189 Req 4.1
    public string? Classification { get; set; } // AcMonoclonal, Fluorocromo, Panel
    
    public string? StorageConditions { get; set; }
    public Guid? DefaultLocationId { get; set; }
    public StorageLocation? DefaultLocation { get; set; }
    public int? OpenShelfLifeDays { get; set; }
    public ReagentStatus Status { get; set; } = ReagentStatus.ACTIVO;
    public decimal MinStock { get; set; }
    public decimal TargetStock { get; set; }
    public decimal ReorderQty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ReagentLot> Lots { get; set; } = new();
}

public class ReagentLot
{
    public Guid Id { get; set; }
    public Guid ReagentId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public DateTime ReceivedDate { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal AvailableQty { get; set; }
    public Guid? LocationId { get; set; }
    public StorageLocation? Location { get; set; }
    public LotStatus Status { get; set; } = LotStatus.RELEASED;
    public DateTime? OpenedDate { get; set; }
    public DateTime? OpenExpiryDate { get; set; }
    
    // ISO 15189 Req 4.3 Trazabilidad
    public Guid? PanelId { get; set; }
    
    public Guid? ReleaseByUserId { get; set; }
    public DateTime? ReleaseAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InventoryMovement
{
    public Guid Id { get; set; }
    public DateTime MovedAt { get; set; }
    public Guid? UserId { get; set; }
    public Guid ReagentId { get; set; }
    public Reagent Reagent { get; set; } = null!;
    public Guid? ReagentLotId { get; set; }
    public ReagentLot? ReagentLot { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public decimal Qty { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
}
