using System;

namespace QMSFlowDoc.Domain.Entities;

public class EquipmentAcceptance
{
    public Guid Id { get; set; } // Key (1-to-1 with Equipment)
    public Guid EquipmentId { get; set; }
    
    // Recepción
    public DateTime? ReceptionDate { get; set; }
    public string? ReceptionCondition { get; set; } // Nuevo, Usado, Reacondicionado
    public bool PackagingCorrect { get; set; } = true;
    public bool VisualDamage { get; set; } = false;
    public bool AccessoriesReceived { get; set; } = true;
    public string? ReceptionNotes { get; set; }
    public string? ReceptionEvidencePath { get; set; }
    
    // Instalación
    public DateTime? InstallationDate { get; set; }
    public string? InstalledBy { get; set; } // Interno, Servicio Técnico, Proveedor
    public bool AmbientConditionsCorrect { get; set; } = true;
    public bool ConnectionsCorrect { get; set; } = true;
    public bool InitialPowerOnCorrect { get; set; } = true;
    public bool SoftwareCommunicationCorrect { get; set; } = true;
    public string? InstallationNotes { get; set; }
    public string? InstallationEvidencePath { get; set; }
    
    // Aceptación
    public DateTime? AcceptanceDate { get; set; }
    public bool CriteriaDefined { get; set; } = true;
    public string? CriteriaMet { get; set; } // Sí, No, Parcial
    public string? AcceptanceOutcome { get; set; } // Aceptado, Aceptado con Restricciones, Rechazado, Pendiente
    public string? AcceptanceRestrictions { get; set; }
    public DateTime? ServiceEntryDate { get; set; }
    public string? AcceptanceEvidencePath { get; set; }
    public string? AcceptanceNotes { get; set; }

    // Navigation
    public Equipment? Equipment { get; set; }
}

public class EquipmentCalibrationPlan
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string ControlledMagnitude { get; set; } = string.Empty; // e.g. Volumen, Temperatura, RCF
    public int FrequencyMonths { get; set; } // Periodicidad en meses
    public DateTime? LastCalibrationDate { get; set; }
    public DateTime? NextCalibrationDate { get; set; }
    public string? Tolerance { get; set; } // Tolerancia admitida
    public string? ProviderOrMethod { get; set; } // Proveedor externo (ej: ENAC) o método interno
    public bool RequiresCertificate { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public Equipment? Equipment { get; set; }
}

public enum CalibrationOutcome
{
    APTO,
    NO_APTO,
    APTO_CON_RESTRICCIONES
}

public class EquipmentCalibrationRecord
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid? PlanId { get; set; }
    
    public DateTime PerformedAt { get; set; }
    public Guid PerformedByUserId { get; set; }
    public string PerformedByUserName { get; set; } = string.Empty;
    
    public string Type { get; set; } = "Calibración"; // Calibración, Verificación
    public string Magnitude { get; set; } = string.Empty; // e.g. Volumen, Temperatura
    public CalibrationOutcome Outcome { get; set; } = CalibrationOutcome.APTO;
    
    public string? ObservedError { get; set; } // Error observado (e.g. 0.15 uL)
    public string? MaxPermissibleError { get; set; } // EMP (e.g. 0.20 uL)
    public string? Uncertainty { get; set; } // Incertidumbre de calibración
    public string? CertificatePath { get; set; } // Enlace al certificado PDF
    public DateTime? NextDueDate { get; set; }
    public string? Restrictions { get; set; }
    public bool ImpactAssessmentRequired { get; set; } = false; // Requiere evaluación de impacto si no es conforme
    public string? Notes { get; set; }

    // Pipette specific fields (nullable)
    public decimal? VolumeNominal { get; set; }
    public decimal? VolumeTested { get; set; }
    public decimal? SystematicError { get; set; }
    public decimal? RandomError { get; set; }
    public decimal? AcceptableLimit { get; set; }
    public string? PointsResultsJson { get; set; } // Resultados detallados por punto calibrado

    // Navigation
    public Equipment? Equipment { get; set; }
}

public class EquipmentRepairRecord
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    
    public DateTime DetectionDate { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public string DetectedDuring { get; set; } = string.Empty; // Uso rutinario, QC, Mantenimiento, Calibración, Revisión externa, Otro
    public string Severity { get; set; } = "Baja"; // Baja, Moderada, Alta, Crítica
    
    public bool EquipmentRemovedFromService { get; set; } = false;
    public DateTime? RemovedFromServiceDate { get; set; }
    public bool TechnicalServiceNotified { get; set; } = false;
    public DateTime? NotificationDate { get; set; }
    
    public DateTime? InterventionDate { get; set; }
    public string? InterventionDescription { get; set; }
    public string? PartsReplaced { get; set; }
    public bool ConfigurationModified { get; set; } = false;
    public bool SoftwareUpdated { get; set; } = false;
    
    public string Outcome { get; set; } = "Resuelto"; // Resuelto, No Resuelto, Parcial
    public bool VerificationRequired { get; set; } = true; // QC/Verificación funcional posterior obligatoria
    public bool VerificationPerformed { get; set; } = false; // ¿Se ha realizado la verificación funcional?
    public DateTime? ReactivationDate { get; set; }
    public EquipmentStatus EndStatus { get; set; } = EquipmentStatus.IN_SERVICE;
    
    public string? EvidencePath { get; set; }
    public string? Notes { get; set; }
    public string? PerformedBy { get; set; } // Nombre del servicio técnico o usuario que lo hizo

    // Navigation
    public Equipment? Equipment { get; set; }
}

public class EquipmentIncident
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    
    public DateTime IncidentDate { get; set; }
    public string IncidentType { get; set; } = string.Empty; // Fallo QC, Fallo mecánico, Fallo fluídico, Fallo óptico, Fallo eléctrico, Fallo informático, Fallo software, Pérdida de datos, Problema exportación FCS, Temperatura fuera rango, Alarma, Daño físico, Otro
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Baja"; // Baja, Moderada, Alta, Crítica
    
    public EquipmentStatus ImmediateStatus { get; set; } = EquipmentStatus.OUT_OF_SERVICE;
    public string ImmediateAction { get; set; } = string.Empty;
    
    public bool RequiresRemoval { get; set; } = false;
    public bool RequiresRepair { get; set; } = false;
    public bool RequiresImpactAssessment { get; set; } = false;
    public bool RequiresNotification { get; set; } = false; // Notificación a proveedor / fabricante
    public string? EvidencePath { get; set; }
    
    public string IncidentStatus { get; set; } = "Abierta"; // Abierta, En investigación, Pendiente de reparación, Pendiente de verificación, Cerrada
    public DateTime? ClosedAt { get; set; }
    public string? Conclusion { get; set; }

    // Navigation
    public Equipment? Equipment { get; set; }
}

public class EquipmentImpactAssessment
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid? IncidentId { get; set; }
    public Guid? RepairId { get; set; }
    
    public DateTime? LastConformingVerificationDate { get; set; }
    public DateTime? ProbableStartDateOfProblem { get; set; }
    public string? PotentiallyAffectedPeriod { get; set; } // e.g. "25/05/2026 al 28/05/2026"
    
    public string ImpactType { get; set; } = "Sin impacto esperado"; // Sin impacto esperado, Posible impacto en funcionamiento, Posible impacto en resultados, Posible impacto en trazabilidad, Posible impacto en conservación, Posible impacto en archivos FCS
    public bool RequiresExternalReview { get; set; } = false;
    public Guid? ExternalNCId { get; set; } // Enlace al módulo de no conformidades si existe
    
    public string Decision { get; set; } = "Sin acciones adicionales"; // Sin acciones adicionales, Mantener equipo restringido, Retirar equipo, Repetir verificación, Escalar a calidad/no conformidad externa, Solicitar revisión retrospectiva externa
    public string Justification { get; set; } = string.Empty;
    public EquipmentStatus EndStatus { get; set; } = EquipmentStatus.IN_SERVICE;
    
    public string? EvidencePath { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Navigation
    public Equipment? Equipment { get; set; }
}

public class EquipmentDecommission
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    
    public string Type { get; set; } = "Cuarentena"; // Cuarentena, Fuera de servicio temporal, Retirada, Obsoleto, Baja definitiva
    public string Reason { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public EquipmentStatus PreviousStatus { get; set; }
    public EquipmentStatus NewStatus { get; set; }
    
    public bool RequiresDecontamination { get; set; } = false;
    public bool DecontaminationPerformed { get; set; } = false;
    public string? DecontaminationEvidencePath { get; set; }
    
    public string Destination { get; set; } = string.Empty; // Almacén, Servicio técnico, Retirada hospitalaria, Eliminación, Devolución a proveedor
    public string? Notes { get; set; }
    
    public Guid ValidatedByUserId { get; set; }
    public string ValidatedByUserName { get; set; } = string.Empty;

    // Navigation
    public Equipment? Equipment { get; set; }
}

public class EquipmentAlert
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // PENDING_ACCEPTANCE, CRITICAL_NO_MAINTENANCE, CRITICAL_NO_CALIBRATION, MAINTENANCE_DUE, MAINTENANCE_OVERDUE, CALIBRATION_DUE, CALIBRATION_OVERDUE, QC_PENDING, QC_FAIL, PENDING_POST_REPAIR_VERIFICATION, OUT_OF_SERVICE, IN_QUARANTINE, RESTRICTED
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "WARNING"; // CRITICAL, WARNING, INFO
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Equipment? Equipment { get; set; }
}

public class EquipmentIndicatorSnapshot
{
    public Guid Id { get; set; }
    public DateTime SnapshotDate { get; set; }
    public string IndicatorKey { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class EquipmentStatusHistory
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public DateTime Date { get; set; }
    public EquipmentStatus OldStatus { get; set; }
    public EquipmentStatus NewStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? EvidencePath { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Equipment? Equipment { get; set; }
}
