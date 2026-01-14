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
public class CompetenciesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CompetenciesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<IEnumerable<CompetencyDto>>> GetCatalog()
    {
        var competencies = await _context.CompetencyCatalogs
            .Where(c => c.IsActive)
            .Select(c => new CompetencyDto(c.Id, c.Code, c.Name, c.Description, c.Area))
            .ToListAsync();

        return Ok(competencies);
    }

    [HttpGet("staff/{staffId}")]
    public async Task<ActionResult<IEnumerable<CompetencyEvaluationDto>>> GetStaffEvaluations(Guid staffId)
    {
        var evaluations = await _context.CompetencyEvaluations
            .Include(e => e.Competency)
            .Where(e => e.StaffId == staffId && e.Status == "ACTIVO")
            .OrderByDescending(e => e.EvaluationDate)
            .Select(e => new CompetencyEvaluationDto(
                e.Id,
                e.CompetencyId,
                e.Competency != null ? e.Competency.Name : "Desconocido",
                e.Competency != null ? e.Competency.Area : null,
                e.EvaluationDate,
                e.ValidUntil,
                e.Outcome,
                "Evaluador"
            ))
            .ToListAsync();

        return Ok(evaluations);
    }

    [HttpDelete("evaluation/{id}")]
    public async Task<IActionResult> DeleteEvaluation(Guid id)
    {
        try
        {
            var evaluation = await _context.CompetencyEvaluations.FindAsync(id);
            if (evaluation == null) return NotFound();

            // Audit log
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"[AUDIT] User {userIdStr} deleted competency evaluation {id} at {DateTime.UtcNow}");

            _context.CompetencyEvaluations.Remove(evaluation);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting evaluation: {ex.Message}");
            return StatusCode(500, $"Error al eliminar evaluación: {ex.Message}");
        }
    }
}
