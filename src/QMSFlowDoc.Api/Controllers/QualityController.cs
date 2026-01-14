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
public class QualityController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public QualityController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("nc")]
    public async Task<ActionResult<IEnumerable<NCListDto>>> GetNonconformities()
    {
        var ncs = await _context.Nonconformities
            .Include(n => n.Actions)
            .OrderByDescending(n => n.DetectedAt)
            .Select(n => new NCListDto(
                n.Id,
                n.DetectedAt,
                n.Title,
                n.Severity,
                n.Status,
                n.ImpactPatient,
                n.Actions.Count,
                n.Origin, // ISO 15189
                n.RootCauseAnalysis // ISO 15189
            ))
            .ToListAsync();

        return Ok(ncs);
    }

    [HttpGet("nc/{id}")]
    public async Task<ActionResult<Nonconformity>> GetNC(Guid id)
    {
        var nc = await _context.Nonconformities
            .Include(n => n.Actions)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (nc == null) return NotFound();

        return Ok(nc);
    }

    [HttpPost("nc")]
    public async Task<ActionResult<Nonconformity>> CreateNC(CreateNCRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        var nc = new Nonconformity
        {
            Title = request.Title,
            Description = request.Description,
            Severity = request.Severity,
            ImpactPatient = request.ImpactPatient,
            Containment = request.Containment,
            Origin = request.Origin, // ISO 15189
            RootCauseAnalysis = request.RootCauseAnalysis, // ISO 15189
            DetectedAt = DateTime.UtcNow,
            DetectedByUserId = userId != null ? Guid.Parse(userId) : null,
            Status = request.Status ?? NCStatus.OPEN
        };

        _context.Nonconformities.Add(nc);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetNC), new { id = nc.Id }, nc);
    }

    [HttpPut("nc/{id}")]
    public async Task<IActionResult> UpdateNC(Guid id, CreateNCRequest request)
    {
        var nc = await _context.Nonconformities.FindAsync(id);
        if (nc == null) return NotFound();

        nc.Title = request.Title;
        nc.Description = request.Description;
        nc.Severity = request.Severity;
        nc.ImpactPatient = request.ImpactPatient;
        nc.Containment = request.Containment;
        nc.Origin = request.Origin; // ISO 15189
        nc.RootCauseAnalysis = request.RootCauseAnalysis; // ISO 15189
        nc.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("nc/{id}/status")]
    public async Task<IActionResult> UpdateNCStatus(Guid id, [FromBody] int status)
    {
        var nc = await _context.Nonconformities.FindAsync(id);
        if (nc == null) return NotFound();

        nc.Status = (NCStatus)status;
        nc.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("capa")]
    public async Task<ActionResult<CapaAction>> CreateCAPA(CreateCAPARequest request)
    {
        var action = new CapaAction
        {
            NCId = request.NCId,
            ActionType = request.ActionType,
            Description = request.Description,
            OwnerUserId = request.OwnerUserId,
            DueDate = request.DueDate,
            Status = CAPAStatus.OPEN
        };

        _context.CapaActions.Add(action);
        await _context.SaveChangesAsync();

        return Ok(action);
    }
}
