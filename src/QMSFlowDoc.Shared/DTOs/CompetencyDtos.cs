using System;

namespace QMSFlowDoc.Shared.DTOs;

public record CompetencyDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Area
);

// Extended DTO for list display with all required columns
public record CompetencyEvaluationDto(
    Guid Id,
    Guid CompetencyId,
    string CompetencyName,
    string? Area,
    DateTime EvaluationDate,
    DateTime? ValidUntil,
    string Outcome,
    string? EvaluatorName
);
