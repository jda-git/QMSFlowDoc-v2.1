using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public class TrainingTypeCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // CURSO, CONGRESO, SESION_INTERNA
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class TrainingActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Provider { get; set; }
    
    public Guid TrainingTypeId { get; set; }
    public TrainingTypeCatalog? TrainingType { get; set; }
    
    public string Modality { get; set; } = "PRESENCIAL"; // PRESENCIAL, ONLINE, MIXTA
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Hours { get; set; }
    public string? Credits { get; set; }
    public string? Description { get; set; }
    
    public bool IsInternal { get; set; }
    public string? InternalDepartment { get; set; }
    
    public string Status { get; set; } = "ACTIVO"; // ACTIVO, ANULADO
    public string? AnnulReason { get; set; }
    
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public List<StaffTraining> Assignments { get; set; } = new();
}

public class StaffTraining
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid StaffId { get; set; }
    public StaffProfile? Staff { get; set; }
    
    public Guid TrainingActivityId { get; set; }
    public TrainingActivity? TrainingActivity { get; set; }
    
    public string ParticipationRole { get; set; } = "ASISTENTE"; // ASISTENTE, PONENTE
    public string Result { get; set; } = "APTO"; // APTO, NO_APTO
    public string? Score { get; set; }
    
    public DateTime? CompletionDate { get; set; }
    public Guid? CertificateDocId { get; set; }
    public string? Notes { get; set; }
    
    public string Status { get; set; } = "ACTIVO";
    public string? AnnulReason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
