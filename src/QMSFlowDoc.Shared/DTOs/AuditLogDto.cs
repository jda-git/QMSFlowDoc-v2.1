using System;

namespace QMSFlowDoc.Shared.DTOs;

public record AuditLogDto(
    Guid Id,
    DateTime Timestamp,
    string? UserId,
    string? UserName,
    string Action,
    string? EntityType,
    string? EntityId,
    string? Details,
    string? MachineName
)
{
    public string FormattedTimestamp => Timestamp.ToString("g");
}

public class AuditFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Action { get; set; }
    public string? UserName { get; set; }
    public string? EntityType { get; set; }
}
