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
public class TrainingController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TrainingController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingActivityDto>>> GetActivities()
    {
        var activities = await _context.TrainingActivities
            .Include(t => t.TrainingType)
            .OrderByDescending(t => t.StartDate)
            .AsNoTracking()
            .ToListAsync();

        return Ok(activities.Select(t => new TrainingActivityDto(
            t.Id,
            t.Title,
            t.Provider,
            t.TrainingType?.Name ?? "General",
            t.StartDate,
            t.Hours,
            t.Status
        )));
    }

    [HttpPost]
    public async Task<ActionResult<TrainingActivity>> CreateActivity(CreateTrainingActivityRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid userId = userIdStr != null ? Guid.Parse(userIdStr) : Guid.Empty;

        // Default type if null (TEMPORARY: Needs seeding or real selection)
        Guid typeId = request.TrainingTypeId ?? Guid.Empty;
        if (typeId == Guid.Empty)
        {
             var defaultType = await _context.TrainingTypeCatalogs.FirstOrDefaultAsync();
             if (defaultType != null) typeId = defaultType.Id;
             else 
             {
                 // Create default type on fly if none prevents crashes
                 var dt = new TrainingTypeCatalog { Name = "General", Code = "GEN" };
                 _context.TrainingTypeCatalogs.Add(dt);
                 await _context.SaveChangesAsync();
                 typeId = dt.Id;
             }
        }

        var activity = new TrainingActivity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Provider = request.Provider,
            TrainingTypeId = typeId,
            Modality = request.Modality ?? "PRESENCIAL",
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Hours = request.Hours,
            Description = request.Description,
            IsInternal = request.IsInternal,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TrainingActivities.Add(activity);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActivities), new { id = activity.Id }, activity);
    }
}
