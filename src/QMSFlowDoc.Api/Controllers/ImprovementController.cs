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
public class ImprovementController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ImprovementController(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Risk Register

    [HttpGet("risks")]
    public async Task<ActionResult<IEnumerable<RiskListDto>>> GetRisks()
    {
        var risks = await _context.Risks
            .OrderByDescending(r => (int)r.Likelihood * (int)r.Impact)
            .Select(r => new RiskListDto(
                r.Id,
                r.Title,
                r.Category,
                (int)r.Likelihood * (int)r.Impact,
                r.Status,
                r.Likelihood,
                r.Impact
            ))
            .ToListAsync();

        return Ok(risks);
    }

    [HttpGet("risks/{id}")]
    public async Task<ActionResult<Risk>> GetRisk(Guid id)
    {
        var risk = await _context.Risks.Include(r => r.Owner).FirstOrDefaultAsync(r => r.Id == id);
        if (risk == null) return NotFound();
        return Ok(risk);
    }

    [HttpPost("risks")]
    public async Task<ActionResult<Risk>> CreateRisk(CreateRiskRequest request)
    {
        var risk = new Risk
        {
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Likelihood = request.Likelihood,
            Impact = request.Impact,
            MitigationPlan = request.MitigationPlan,
            OwnerUserId = request.OwnerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = RiskStatus.ACTIVE
        };

        _context.Risks.Add(risk);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRisk), new { id = risk.Id }, risk);
    }

    [HttpPut("risks/{id}")]
    public async Task<IActionResult> UpdateRisk(Guid id, CreateRiskRequest request)
    {
        var risk = await _context.Risks.FindAsync(id);
        if (risk == null) return NotFound();

        risk.Title = request.Title;
        risk.Description = request.Description;
        risk.Category = request.Category;
        risk.Likelihood = request.Likelihood;
        risk.Impact = request.Impact;
        risk.MitigationPlan = request.MitigationPlan;
        risk.OwnerUserId = request.OwnerUserId;
        risk.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("risks/{id}/status")]
    public async Task<IActionResult> UpdateRiskStatus(Guid id, [FromBody] int status)
    {
        var risk = await _context.Risks.FindAsync(id);
        if (risk == null) return NotFound();

        risk.Status = (RiskStatus)status;
        risk.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    #region Internal Audits

    [HttpGet("audits")]
    public async Task<ActionResult<IEnumerable<AuditListDto>>> GetAudits()
    {
        var audits = await _context.AuditPlans
            .Include(a => a.Findings)
            .OrderByDescending(a => a.ScheduledDate)
            .Select(a => new AuditListDto(
                a.Id,
                a.Title,
                a.ScheduledDate,
                a.Status,
                a.Findings.Count
            ))
            .ToListAsync();

        return Ok(audits);
    }

    [HttpGet("audits/{id}")]
    public async Task<ActionResult<AuditPlan>> GetAudit(Guid id)
    {
        var audit = await _context.AuditPlans
            .Include(a => a.Findings)
            .Include(a => a.ReportDocument)
            .FirstOrDefaultAsync(a => a.Id == id);
            
        if (audit == null) return NotFound();
        return Ok(audit);
    }

    [HttpPost("audits")]
    public async Task<ActionResult<AuditPlan>> CreateAudit(CreateAuditRequest request)
    {
        var audit = new AuditPlan
        {
            Title = request.Title,
            ScheduledDate = request.ScheduledDate,
            Scope = request.Scope,
            LeadAuditor = request.LeadAuditor,
            Status = AuditStatus.PLANNED,
            ReportDocumentId = request.ReportDocumentId
        };

        _context.AuditPlans.Add(audit);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAudit), new { id = audit.Id }, audit);
    }

    [HttpPut("audits/{id}")]
    public async Task<IActionResult> UpdateAudit(Guid id, CreateAuditRequest request)
    {
        var audit = await _context.AuditPlans.FindAsync(id);
        if (audit == null) return NotFound();

        audit.Title = request.Title;
        audit.ScheduledDate = request.ScheduledDate;
        audit.Scope = request.Scope;
        audit.LeadAuditor = request.LeadAuditor;
        if (request.ReportDocumentId.HasValue) audit.ReportDocumentId = request.ReportDocumentId;
        // Status updates tracked separately or implicitly? keeping simple for now.

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("findings")]
    public async Task<ActionResult<AuditFinding>> RegisterFinding(RegisterFindingRequest request)
    {
        var finding = new AuditFinding
        {
            AuditPlanId = request.AuditPlanId,
            Description = request.Description,
            IsoRequirement = request.IsoRequirement,
            Type = request.Type,
            RelatedNCId = request.RelatedNCId
        };

        _context.AuditFindings.Add(finding);
        await _context.SaveChangesAsync();
        return Ok(finding);
    }

    #endregion

    #region Management Reviews

    [HttpGet("reviews")]
    public async Task<ActionResult<IEnumerable<ManagementReviewListDto>>> GetReviews()
    {
        var reviews = await _context.ManagementReviews
            .OrderByDescending(r => r.ReviewDate)
            .Select(r => new ManagementReviewListDto(
                r.Id,
                r.ReviewDate,
                r.Summary.Length > 100 ? r.Summary.Substring(0, 100) + "..." : r.Summary
            ))
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpGet("reviews/{id}")]
    public async Task<ActionResult<ManagementReview>> GetReview(Guid id)
    {
        var review = await _context.ManagementReviews
            .Include(r => r.MinutesDocument)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (review == null) return NotFound();
        return Ok(review);
    }

    [HttpPost("reviews")]
    public async Task<ActionResult<ManagementReview>> CreateReview(CreateManagementReviewRequest request)
    {
        var review = new ManagementReview
        {
            ReviewDate = request.ReviewDate,
            Participants = request.Participants,
            Agenda = request.Agenda,
            Summary = request.Summary,
            Actions = request.Actions,
            MinutesDocumentId = request.MinutesDocumentId
        };

        _context.ManagementReviews.Add(review);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetReview), new { id = review.Id }, review);
    }

    [HttpPut("reviews/{id}")]
    public async Task<IActionResult> UpdateReview(Guid id, CreateManagementReviewRequest request)
    {
        var review = await _context.ManagementReviews.FindAsync(id);
        if (review == null) return NotFound();

        review.ReviewDate = request.ReviewDate;
        review.Participants = request.Participants;
        review.Agenda = request.Agenda;
        review.Summary = request.Summary;
        review.Actions = request.Actions;
        if (request.MinutesDocumentId.HasValue) review.MinutesDocumentId = request.MinutesDocumentId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    #endregion
}
