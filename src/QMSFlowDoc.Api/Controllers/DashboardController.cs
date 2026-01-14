using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System.Security.Claims;

namespace QMSFlowDoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDataDto>> GetDashboard()
    {
        var totalDocs = await _context.Documents.CountAsync(d => d.Status != DocumentStatus.OBSOLETE && d.Status != DocumentStatus.RETIRED);
        var pendingReview = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.REVIEW);
        var pendingApproval = await _context.Documents.CountAsync(d => d.Status == DocumentStatus.DRAFT);
        
        // Fix: Query reagents directly to include those with 0 lots (0 stock)
        // Client eval required for complex sum if database provider doesn't support subquery sum easily, 
        // but simple Calculate available stock logic:
        var serverReagents = await _context.Reagents
            .Include(r => r.Lots)
            .Where(r => r.Status == ReagentStatus.ACTIVO)
            .Select(r => new 
            { 
                Min = r.MinStock, 
                // Sum only Released lots
                Current = r.Lots.Where(l => l.Status == LotStatus.RELEASED).Sum(l => l.AvailableQty) 
            })
            .ToListAsync();

        var lowStock = serverReagents.Count(x => x.Current <= x.Min);

        var maintenanceDue = await _context.Equipment
            .Include(e => e.MaintenanceEvents)
            .CountAsync(e => e.Status == EquipmentStatus.ACTIVE); // Simplified for MVP

        var highRisks = await _context.Risks
            .CountAsync(r => r.Status == RiskStatus.ACTIVE && (int)r.Likelihood * (int)r.Impact >= 15);

        var staffCount = await _context.StaffProfiles.CountAsync(s => s.IsActive);

        var stats = new DashboardStatsDto(
            totalDocs,
            pendingReview,
            pendingApproval,
            lowStock,
            maintenanceDue,
            highRisks,
            staffCount
        );

        // Recent activity (Last 10 audit logs or movements - simplified)
        var recentMovements = await _context.InventoryMovements
            .OrderByDescending(m => m.MovedAt)
            .Take(5)
            .Select(m => new DashboardRecentActivityDto(
                "INVENTORY",
                $"{m.MovementType}: {m.Qty} unidades de reactivo",
                m.MovedAt,
                "Sistema"
            ))
            .ToListAsync();

        return Ok(new DashboardDataDto(stats, recentMovements));
    }
}
