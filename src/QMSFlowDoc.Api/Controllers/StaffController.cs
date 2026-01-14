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
public class StaffController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StaffController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffListDto>>> GetStaff()
    {
        var staff = await _context.StaffProfiles
            .Include(s => s.User).ThenInclude(u => u!.Roles)
            .Include(s => s.Trainings)
            .Include(s => s.CompetencyEvaluations)
            .OrderBy(s => s.User!.FullName)
            .ToListAsync();

        var result = staff.Select(s => new StaffListDto(
                s.Id,
                s.User?.FullName ?? "N/A",
                s.PositionTitle,
                s.Department,
                s.IsActive,
                s.Trainings.Count,
                s.CompetencyEvaluations.Count(a => a.Outcome == "COMPETENTE"),
                s.User?.Roles.FirstOrDefault()?.RoleName
            ))
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id}/details")]
    public async Task<ActionResult<StaffProfileDetailDto>> GetStaffDetails(Guid id)
    {
        var profile = await _context.StaffProfiles
            .Include(s => s.User).ThenInclude(u => u!.Roles)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (profile == null) return NotFound();

        return Ok(new StaffProfileDetailDto(
            profile.Id,
            profile.UserId ?? Guid.Empty,
            profile.User?.Username ?? "",
            profile.User?.FullName ?? "",
            profile.User?.Email,
            profile.PositionTitle,
            profile.Department,
            profile.HiredAt,
            profile.User?.Roles.FirstOrDefault()?.RoleName ?? "Consultor",
            profile.IsActive
        ));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StaffProfile>> GetStaffProfile(Guid id)
    {
        var profile = await _context.StaffProfiles
            .Include(s => s.User!).ThenInclude(u => u.Roles)
            .Include(s => s.Trainings).ThenInclude(st => st.TrainingActivity)
            .Include(s => s.CompetencyEvaluations).ThenInclude(ce => ce.Competency)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (profile == null) return NotFound();

        return Ok(profile);
    }

    [HttpPost]
    public async Task<ActionResult<StaffProfile>> CreateStaffProfile(CreateStaffProfileRequest request)
    {
        try
        {
            var hiredAt = request.HiredAt;
            if (hiredAt.HasValue && hiredAt.Value.Kind != DateTimeKind.Utc)
            {
                hiredAt = DateTime.SpecifyKind(hiredAt.Value, DateTimeKind.Utc);
            }

            var profile = new StaffProfile
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PositionTitle = request.PositionTitle,
                Department = request.Department,
                HiredAt = hiredAt,
                IsActive = true
            };

            _context.StaffProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStaffProfile), new { id = profile.Id }, profile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating staff profile: {ex.Message}");
            return StatusCode(500, $"Error interno al crear ficha: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStaffProfile(Guid id, UpdateStaffProfileRequest request)
    {
        try
        {
            if (id != request.Id) return BadRequest("ID mismatch");

            var profile = await _context.StaffProfiles.Include(s => s.User!).ThenInclude(u => u!.Roles).FirstOrDefaultAsync(s => s.Id == id);
            if (profile == null) return NotFound();

            var hiredAt = request.HiredAt;
            if (hiredAt.HasValue && hiredAt.Value.Kind != DateTimeKind.Utc)
            {
                hiredAt = DateTime.SpecifyKind(hiredAt.Value, DateTimeKind.Utc);
            }

            profile.PositionTitle = request.PositionTitle;
            profile.Department = request.Department;
            profile.HiredAt = hiredAt;
            profile.IsActive = request.IsActive;

            if (profile.User != null)
            {
                profile.User.FullName = request.FullName;
                profile.User.Email = request.Email;
                
                // Update Role
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == request.RoleName);
                if (role != null)
                {
                    profile.User.Roles.Clear();
                    profile.User.Roles.Add(role);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(profile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating staff profile: {ex.Message}");
            return StatusCode(500, $"Error al actualizar ficha: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStaffProfile(Guid id)
    {
        var profile = await _context.StaffProfiles.FindAsync(id);
        if (profile == null) return NotFound();

        // If it has an associated user, we might want to delete it too or just disassociate
        if (profile.UserId.HasValue)
        {
            var user = await _context.Users.FindAsync(profile.UserId.Value);
            if (user != null)
            {
                _context.Users.Remove(user);
            }
        }

        _context.StaffProfiles.Remove(profile);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("training")]
    public async Task<ActionResult> RegisterTraining(RegisterTrainingRequest request)
    {
        try
        {
            // Get current user for CreatedByUserId
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid creatorId = userIdStr != null ? Guid.Parse(userIdStr) : Guid.Empty;

            // Get or create a default training type
            var trainingType = await _context.TrainingTypeCatalogs.FirstOrDefaultAsync();
            if (trainingType == null)
            {
                trainingType = new TrainingTypeCatalog
                {
                    Id = Guid.NewGuid(),
                    Code = "CURSO",
                    Name = "Curso"
                };
                _context.TrainingTypeCatalogs.Add(trainingType);
                await _context.SaveChangesAsync();
            }

            // Create Training Activity from free-text input
            var activity = new TrainingActivity
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Provider = request.Provider,
                Hours = request.Hours,
                Description = request.Notes,
                TrainingTypeId = trainingType.Id,
                CreatedByUserId = creatorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.TrainingActivities.Add(activity);

            // Create StaffTraining record linking staff to activity
            var completionDate = DateTime.SpecifyKind(request.CompletedAt, DateTimeKind.Utc);
            var training = new StaffTraining
            {
                Id = Guid.NewGuid(),
                StaffId = request.StaffId,
                TrainingActivityId = activity.Id,
                CompletionDate = completionDate,
                Result = request.Result,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.StaffTrainings.Add(training);
            await _context.SaveChangesAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering training: {ex.Message}");
            return StatusCode(500, $"Error al registrar formación: {ex.Message}");
        }
    }

    [HttpDelete("training/{id}")]
    public async Task<IActionResult> DeleteTraining(Guid id)
    {
        try
        {
            var training = await _context.StaffTrainings.FindAsync(id);
            if (training == null) return NotFound();

            // Audit log
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"[AUDIT] User {userIdStr} deleted training {id} at {DateTime.UtcNow}");

            _context.StaffTrainings.Remove(training);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting training: {ex.Message}");
            return StatusCode(500, $"Error al eliminar formación: {ex.Message}");
        }
    }

    [HttpPost("assess")]
    public async Task<ActionResult<CompetencyEvaluation>> AssessCompetency(AssessCompetencyRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? userId = userIdStr != null ? Guid.Parse(userIdStr) : null;
            Guid evaluatorStaffId = Guid.Empty;

            if (userId.HasValue)
            {
                var evaluator = await _context.StaffProfiles.FirstOrDefaultAsync(s => s.UserId == userId);
                if (evaluator != null) evaluatorStaffId = evaluator.Id;
            }

            // Find or create competency catalog entry
            var competency = await _context.CompetencyCatalogs
                .FirstOrDefaultAsync(c => c.Name == request.CompetencyName && c.Area == request.Area);
            
            if (competency == null)
            {
                competency = new CompetencyCatalog
                {
                    Id = Guid.NewGuid(),
                    Code = $"COMP-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Name = request.CompetencyName,
                    Area = request.Area,
                    CreatedByUserId = userId ?? Guid.Empty,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CompetencyCatalogs.Add(competency);
            }

            string outcomeStr = request.Outcome switch
            {
                CompetencyOutcome.PASS => "COMPETENTE",
                CompetencyOutcome.FAIL => "NO_COMPETENTE",
                CompetencyOutcome.CONDITIONAL => "EN_FORMACION",
                _ => "EN_FORMACION"
            };

            var evalDate = DateTime.SpecifyKind(request.EvaluationDate, DateTimeKind.Utc);
            var validUntil = request.ValidUntil.HasValue 
                ? DateTime.SpecifyKind(request.ValidUntil.Value, DateTimeKind.Utc) 
                : (DateTime?)null;
            
            var assessment = new CompetencyEvaluation
            {
                Id = Guid.NewGuid(),
                StaffId = request.StaffId,
                CompetencyId = competency.Id,
                EvaluationDate = evalDate,
                EvaluatorStaffId = evaluatorStaffId,
                Outcome = outcomeStr,
                ValidUntil = validUntil,
                Findings = request.Evidence,
                CreatedAt = DateTime.UtcNow
            };

            _context.CompetencyEvaluations.Add(assessment);

            // Update Materialized Status
            var status = await _context.StaffCompetencyStatuses
                .FirstOrDefaultAsync(s => s.StaffId == request.StaffId && s.CompetencyId == competency.Id);
                
            if (status == null)
            {
                status = new StaffCompetencyStatus
                {
                    StaffId = request.StaffId,
                    CompetencyId = competency.Id
                };
                _context.StaffCompetencyStatuses.Add(status);
            }

            status.CurrentStatus = outcomeStr == "COMPETENTE" ? "APTO" : "NO_APTO";
            status.LastEvaluationId = assessment.Id;
            status.LastEvaluationDate = assessment.EvaluationDate;
            status.NextDueDate = validUntil;
            status.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(assessment);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error assessing competency: {ex.Message}");
            return StatusCode(500, $"Error al evaluar competencia: {ex.Message}");
        }
    }
}
