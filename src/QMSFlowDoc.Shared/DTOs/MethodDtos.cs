using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class MethodDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public MethodStatus Status { get; set; }
    public string StatusDisplay { get; set; } = string.Empty;
    public string? CurrentVersion { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public int AuthorizedUsersCount { get; set; }
    
    public string AuthorizedUsersCountDisplay => AuthorizedUsersCount == 0 ? "Sin autorizados" : $"{AuthorizedUsersCount} autorizados";
}

public record CreateMethodRequest(
    string Code,
    string Name,
    string? Category,
    string? CurrentVersion,
    Guid? DocumentId,
    string? Notes
);

public record UpdateMethodRequest(
    Guid Id,
    string Code,
    string Name,
    string? Category,
    MethodStatus Status,
    string? CurrentVersion,
    DateTime? EffectiveDate,
    Guid? DocumentId,
    string? Notes
);

public record AuthorizeMethodRequest(
    Guid MethodId,
    Guid UserId,
    DateTime? ExpiresAt,
    Guid? AuthorizedByUserId
);

public class MethodAuthorizationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime AuthorizedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? AuthorizedByName { get; set; }
    
    public string AuthorizedAtDisplay => AuthorizedAt.ToString("d");
    public string ExpiresAtDisplay => ExpiresAt.HasValue ? $"Expira: {ExpiresAt.Value:d}" : "Sin Expiración";
}
