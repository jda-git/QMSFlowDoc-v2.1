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
    string? Result,
    Guid? CompetencyId = null
);

public class GlobalTrainingDto
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffPosition { get; set; } = string.Empty;
    public Guid TrainingActivityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public DateTime CompletionDate { get; set; }
    public decimal Hours { get; set; }
    public string? Result { get; set; }
    public string? CompetencyNameRef { get; set; }
    public Guid? CompetencyId { get; set; }
}

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
