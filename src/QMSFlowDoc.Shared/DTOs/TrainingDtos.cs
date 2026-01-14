using System;

namespace QMSFlowDoc.Shared.DTOs;

public record TrainingActivityDto(
    Guid Id,
    string Title,
    string? Provider,
    string? TrainingTypeName,
    DateTime? StartDate,
    decimal Hours,
    string Status
);

// DTO for staff training list display with all required columns
public record StaffTrainingDto(
    Guid Id,
    Guid TrainingActivityId,
    string Title,
    string? Provider,
    DateTime CompletionDate,
    decimal Hours,
    string? Result
);

public record CreateTrainingActivityRequest(
    string Title,
    string? Provider,
    Guid? TrainingTypeId,
    string? Modality, // PRESENCIAL, ONLINE
    DateTime? StartDate,
    DateTime? EndDate,
    decimal Hours,
    string? Description,
    bool IsInternal
);
