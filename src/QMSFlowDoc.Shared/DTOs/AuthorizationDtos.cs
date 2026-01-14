using System;

namespace QMSFlowDoc.Shared.DTOs;

public record AuthorizationDto(
    Guid Id,
    string Code,
    string Name,
    string? Description
);

// Extended DTO for list display with all required columns
public record StaffAuthorizationDto(
    Guid Id,
    Guid AuthorizationId,
    string AuthorizationName,
    string? Description,
    DateTime ValidFrom,
    DateTime? ValidUntil,
    DateTime GrantedAt,
    string Status,
    string? GrantedByName  // Nombre del usuario que emitió la autorización
);
