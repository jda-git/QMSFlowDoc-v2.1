using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public enum EquipmentStatus
{
    IN_SERVICE = 0,
    ACTIVE = 0,                         // Alias for backwards compatibility
    PENDING_QC_VERIFICATION = 1,
    PENDING_VERIFICATION = 1,           // Alias for backwards compatibility
    OUT_OF_SERVICE = 2,                 // Bloqueado por fallo (backwards-compatible with OUT_OF_SERVICE)
    RETIRED = 3,                        // Retirado del servicio (backwards-compatible with RETIRED)
    PENDING_RECEIPT = 4,                // Pendiente de recepción
    RECEIVED = 5,                       // Recibido
    PENDING_ACCEPTANCE = 6,             // Pendiente de aceptación
    ACCEPTED = 7,                       // Aceptado
    IN_SERVICE_WITH_RESTRICTIONS = 8,   // En servicio con restricciones
    QC_NON_CONFORMING = 9,              // QC/verificación no conforme
    IN_MAINTENANCE = 10,                // En mantenimiento preventivo
    IN_REPAIR = 11,                     // En reparación / correctivo
    IN_QUARANTINE = 12,                 // En cuarentena
    OBSOLETE = 13,                      // Obsoleto
    DECOMMISSIONED = 14                 // Baja definitiva
}

public enum EquipmentCriticidad
{
    CRITICAL,      // Afecta directamente a resultados, trazabilidad o conservación crítica
    SEMICRITICAL,  // Afecta indirectamente al proceso
    AUXILIARY,     // Apoyo operativo, bajo impacto
    NON_CRITICAL   // Inventariable, sin impacto relevante
}

public enum EquipmentAptitude
{
    APTO,
    NO_APTO,
    CON_RESTRICCIONES
}

public enum MaintenanceEventType
{
    PREVENTIVE,
    CORRECTIVE,
    INSPECTION,
    CALIBRATION,
    VERIFICATION,
    CLEANING
}

public class Equipment
{
    public Guid Id { get; set; }
    public string? InternalId { get; set; } // Lab ID (e.g. EQ-001)
    public string? AssetTag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SoftwareVersion { get; set; } // ISO 15189 Req 3.1
    public string? FirmwareVersion { get; set; } // ISO 15189 Req 3.1
    public string? Location { get; set; }
    
    // ISO 15189:2022 Lifecycle & Context
    public EquipmentStatus Status { get; set; } = EquipmentStatus.PENDING_RECEIPT;
    public EquipmentCriticidad Criticidad { get; set; } = EquipmentCriticidad.SEMICRITICAL;
    public EquipmentAptitude Aptitude { get; set; } = EquipmentAptitude.NO_APTO;
    public string? Restrictions { get; set; } // Detalle de restricciones si las hubiera
    public string? HospitalInventoryNumber { get; set; }
    public string? AreaLaboratorio { get; set; } // Hematología, Inmunología, etc.
    public string? Description { get; set; }
    public string? IntendedUse { get; set; } // Uso previsto
    
    // Fechas de ciclo de vida
    public DateTime? PurchaseDate { get; set; }
    public DateTime? InstalledAt { get; set; }
    public DateTime? ReceptionDate { get; set; }
    public string? ReceptionCondition { get; set; } // Nuevo, Usado, Reacondicionado
    public DateTime? AcceptanceDate { get; set; }
    public DateTime? ServiceEntryDate { get; set; }
    public DateTime? DecommissionDate { get; set; }
    public DateTime? WarrantyUntil { get; set; }
    public bool HasMaintenanceContract { get; set; } = false;
    public string? TechnicalServiceInfo { get; set; } // Proveedor / Teléfono / Datos del servicio técnico
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Metrology tracking
    public DateTime? VerificationDate { get; set; }
    public bool IsVerified { get; set; } = false;
    public int? CalibrationFrequencyMonths { get; set; }
    public DateTime? LastCalibration { get; set; }
    public DateTime? NextCalibration { get; set; }
    public string? ManualPath { get; set; } // Path o enlace al manual PDF
    
    // Impacto potencial (Flags de criticidad)
    public bool ImpactResult { get; set; } = false;
    public bool ImpactPreservation { get; set; } = false;
    public bool ImpactTraceability { get; set; } = false;
    public bool ImpactBiosecurity { get; set; } = false;
    public bool ImpactContinuity { get; set; } = false;
    public bool ImpactPreparation { get; set; } = false;
    public bool ImpactAnalysis { get; set; } = false;

    // ── Especificaciones técnicas variables según tipo de equipo ──

    // Para Citómetros
    public string? CytoType { get; set; } // Convencional, Espectral, Sorter, Otro
    public int? CytoLasersCount { get; set; }
    public string? CytoWavelengths { get; set; } // e.g. "488nm, 638nm, 405nm"
    public int? CytoDetectorsCount { get; set; }
    public string? CytoOpticalConfig { get; set; }
    public string? CytoFilters { get; set; }
    public string? CytoParameters { get; set; } // Parámetros que mide
    public string? CytoAcquisitionSoftware { get; set; }
    public string? CytoAcquisitionSoftwareVersion { get; set; }
    public string? CytoAssociatedComputer { get; set; }
    public string? CytoOS { get; set; }
    public string? CytoAcquisitionConfig { get; set; }
    public string? CytoQcConfig { get; set; }
    public bool CytoFcsExport { get; set; } = false;
    public string? CytoFcsExportPath { get; set; }
    public string? CytoNotes { get; set; }

    // Para Pipetas
    public string? PipetteType { get; set; } // Monocanal, Multicanal, Repetidora, etc.
    public decimal? PipetteNominalVolume { get; set; } // e.g. 100 uL
    public string? PipetteVolumeRange { get; set; } // e.g. "10-100 uL"
    public decimal? PipetteResolution { get; set; } // e.g. 0.1 uL
    public bool PipetteCriticalUse { get; set; } = false;
    public string? PipetteIntendedUse { get; set; }
    public string? PipetteEmpLimit { get; set; } // Error Máximo Permitido interno (e.g. 1%)

    // Para Neveras/Congeladores
    public decimal? ColdTempMin { get; set; } // Temp mínima admisible
    public decimal? ColdTempMax { get; set; } // Temp máxima admisible
    public string? ColdTempRecordType { get; set; } // Manual, Automático continuo, Sonda inalámbrica
    public string? ColdSensorAssociated { get; set; } // Código de la sonda
    public bool ColdHasAlarm { get; set; } = false;
    public bool ColdHasBackup { get; set; } = false;

    // Para Centrífugas
    public string? CentrifugeType { get; set; } // Refrigerada, Microcentrífuga, de tubos, etc.
    public string? CentrifugeRotor { get; set; } // Modelo del rotor
    public string? CentrifugeRpmRange { get; set; } // Rango RPM
    public string? CentrifugeRcfRange { get; set; } // Rango RCF
    public bool CentrifugeHasTimer { get; set; } = false;
    public string? CentrifugeSpecificMaintenance { get; set; }

    // Para Software inventariado como equipamiento
    public string? SoftName { get; set; }
    public string? SoftManufacturer { get; set; }
    public string? SoftVersion { get; set; }
    public string? SoftLicense { get; set; }
    public string? SoftComputerInstalled { get; set; }
    public string? SoftFunction { get; set; } // Adquisición, Análisis, LIS, Interfaz, Backup
    public DateTime? SoftInstallationDate { get; set; }
    public string? SoftValidationState { get; set; } // Validado / Pendiente de verificación funcional

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Relaciones
    public List<MaintenancePlan> MaintenancePlans { get; set; } = new();
    public List<MaintenanceEvent> MaintenanceEvents { get; set; } = new();
    public List<EquipmentFunctionalQC> FunctionalQCs { get; set; } = new();
}

public class MaintenancePlan
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int FrequencyDays { get; set; }
    public string ChecklistJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    
    // ISO 15189 Extensions
    public DateTime? StartDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public int? ToleranceDays { get; set; } // Tolerancia en días
    public string? Responsible { get; set; } // Interno / Externo
    public bool RequiresStop { get; set; } = false;
    public bool RequiresVerification { get; set; } = false;
}

public class MaintenanceEvent
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid? PlanId { get; set; }
    public string? PlanName { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime PerformedAt { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByUserName { get; set; }
    public MaintenanceEventType EventType { get; set; } = MaintenanceEventType.PREVENTIVE;
    public bool IsInternal { get; set; } = true;
    public string? ActivitiesPerformed { get; set; }
    public string? Outcome { get; set; }
    public bool HasDeviation { get; set; } = false;
    public string? DeviationReason { get; set; }
    public bool RequiresAdditionalAction { get; set; } = false;
    public bool RequiresVerification { get; set; } = false;
    public bool VerificationPerformed { get; set; } = false;
    public EquipmentStatus? EndStatus { get; set; }
    public string? Notes { get; set; }
    public Guid? EvidenceDocId { get; set; }
    public string? CertificatePath { get; set; }
    public decimal? Cost { get; set; }
    public bool IsEfficiencyCheck { get; set; }
    public bool? HasIssues { get; set; }
    public int? NextMaintenanceMonth { get; set; }
    public int? NextMaintenanceYear { get; set; }

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class EquipmentHistory
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public DateTime Date { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }
    public string? EvidencePath { get; set; }
}
