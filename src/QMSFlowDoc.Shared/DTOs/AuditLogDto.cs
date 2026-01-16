using System;

namespace QMSFlowDoc.Shared.DTOs;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }

    public string FormattedTimestamp => Timestamp.ToString("g"); // 15/01/2026 14:30
}
