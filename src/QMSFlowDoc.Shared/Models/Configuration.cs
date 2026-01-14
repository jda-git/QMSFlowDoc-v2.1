using System;

namespace QMSFlowDoc.Shared.Models;

public class ReagentType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class SystemSetting
{
    public string Key { get; set; } = string.Empty; // PK
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}
