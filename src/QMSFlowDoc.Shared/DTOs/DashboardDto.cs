using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.DTOs;

public class DashboardStatsDto
{
    public int TotalDocuments { get; set; }
    public int PendingReviewDocs { get; set; }
    public int PendingApprovalDocs { get; set; }
    public int LowStockReagents { get; set; }
    public int ExpiringReagents { get; set; }
    public int DueEquipmentMaintenance { get; set; }
    public int OpenHighRisks { get; set; }
    public int ActiveStaffCount { get; set; }
    
    // New KPIs
    public int ActiveEQAPrograms { get; set; }
    public int PendingEQAResults { get; set; }
    public int ActiveMethods { get; set; }
    public int ExpiringAuthorizations { get; set; }
    public int ExpiredCompetencies { get; set; }
    public int PendingTrainings { get; set; }

    public DashboardStatsDto() { }
}


public class DashboardRecentActivityDto
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserName { get; set; } = string.Empty;

    public DashboardRecentActivityDto() { }
    public DashboardRecentActivityDto(string type, string desc, DateTime ts, string user)
    {
        Type = type; Description = desc; Timestamp = ts; UserName = user;
    }
}

public record DashboardDataDto(
    DashboardStatsDto Stats,
    List<DashboardRecentActivityDto> RecentActivity
);

