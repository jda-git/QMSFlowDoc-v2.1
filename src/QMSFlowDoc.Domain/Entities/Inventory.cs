using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QMSFlowDoc.Domain.Entities;

public enum ReagentStatus
{
    ACTIVO,
    EN_CAMBIO,
    OBSOLETO,
    BLOQUEADO
}

public enum LotStatus
{
    QUARANTINE = 0,
    RELEASED = 1,
    BLOCKED = 2,
    CONSUMED = 3,
    EXPIRED = 4,
    RECALLED = 5,
    IN_USE = 6
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

// ISO 15189:2022 - Supplier Types (Apartado 6.8)
public enum SupplierType
{
    SUMINISTROS_REACTIVOS,
    SERVICIO_TECNICO,
    LABORATORIO_DERIVACION
}

// ISO 15189:2022 - Supplier Quality Status
public enum SupplierQualityStatus
{
    PENDIENTE,
    APTO,
    NO_APTO,
    EN_OBSERVACION,
    EVALUACION_CADUCADA
}

public class Supplier
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    
    // ISO 15189:2022 Evaluation Fields
    public SupplierType Type { get; set; } = SupplierType.SUMINISTROS_REACTIVOS;
    public SupplierQualityStatus QualityStatus { get; set; } = SupplierQualityStatus.PENDIENTE;
    public DateTime? LastEvaluationDate { get; set; }
    public DateTime? NextEvaluationDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<SupplierEvaluation> Evaluations { get; set; } = new();
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
    public string? Fluorescence { get; set; }
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
    public bool IsDeleted { get; set; } = false;

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

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

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
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
