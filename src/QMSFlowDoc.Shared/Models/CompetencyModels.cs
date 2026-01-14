using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public class CompetencyCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // Unique code
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleScope { get; set; } = "AMBOS";
    public string Area { get; set; } = string.Empty; // PREANALITICA, ANALITICA, etc.
    public string? SubArea { get; set; }
    public int DefaultReassessmentMonths { get; set; } = 12;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}

public class CompetencyEvalTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompetencyId { get; set; }
    public CompetencyCatalog? Competency { get; set; }
    public int Version { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string RubricJson { get; set; } = "{}"; // JSON criteria
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? ObsoleteFrom { get; set; }
    public Guid ApprovedByUserId { get; set; }
}

public class CompetencyAssessmentMethod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // OBSERVACION, REVISION, etc.
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class CompetencyEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffId { get; set; }
    public StaffProfile? Staff { get; set; }
    
    public Guid CompetencyId { get; set; }
    public CompetencyCatalog? Competency { get; set; }
    
    public Guid? TemplateId { get; set; }
    public CompetencyEvalTemplate? Template { get; set; }
    
    public DateTime EvaluationDate { get; set; }
    public Guid EvaluatorStaffId { get; set; }
    
    public string Outcome { get; set; } = "COMPETENTE"; // COMPETENTE, NO_COMPETENTE, EN_FORMACION
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextDueDate { get; set; }
    
    public string? Findings { get; set; }
    public string? CorrectiveActions { get; set; }
    
    public Guid? EvidenceDocId { get; set; }
    
    public string Status { get; set; } = "ACTIVO"; // ACTIVO, ANULADO
    public string? AnnulReason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // M:N Methods used
    public List<CompetencyAssessmentMethod> MethodsUsed { get; set; } = new();
}

public class StaffCompetencyStatus
{
    public Guid StaffId { get; set; }
    public StaffProfile? Staff { get; set; }
    
    public Guid CompetencyId { get; set; }
    public CompetencyCatalog? Competency { get; set; }
    
    public string CurrentStatus { get; set; } = "SIN_EVIDENCIA"; // APTO, NO_APTO, CADUCADA...
    public Guid? LastEvaluationId { get; set; }
    public CompetencyEvaluation? LastEvaluation { get; set; }
    
    public DateTime? LastEvaluationDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
