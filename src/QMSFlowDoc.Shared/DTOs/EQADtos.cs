using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public record EQAProgramDto(
    Guid Id,
    string Name,
    string? Provider,
    string? Frequency,
    EQAStatus Status,
    string? LastResult, // e.g. "SATISFACTORY (2024-01)"
    string? LastResultColor, // Green/Red
    int PendingCount
);

public record CreateEQAProgramRequest(
    string Name,
    string? Provider,
    string? Frequency,
    string? Notes
);

public record UpdateEQAProgramRequest(
    Guid Id,
    string Name,
    string? Provider,
    string? Frequency,
    EQAStatus Status,
    string? Notes
);

public record RegisterEQAResultRequest(
    Guid ProgramId,
    string CycleIdentifier,
    DateTime? ReceiptDate,
    DateTime? ProcessingDate,
    DateTime? SubmissionDate,
    string? Notes
);

public record UpdateEQAResultRequest(
    Guid Id,
    EQAResultStatus Status,
    decimal? Score,
    EQAPerformance Performance,
    string? Notes,
    Guid? ReviewerUserId,
    DateTime? ReviewDate
);

public record EQAResultDto(
    Guid Id,
    Guid ProgramId,
    string CycleIdentifier,
    string Status,
    string Performance,
    string PerformanceColor,
    DateTime? SubmissionDate,
    decimal? Score,
    string? Notes
);
