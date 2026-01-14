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
public class AuthorizationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AuthorizationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<IEnumerable<AuthorizationDto>>> GetCatalog()
    {
        var catalog = await _context.AuthorizationCatalogs
            .Where(a => a.IsActive)
            .Select(a => new AuthorizationDto(a.Id, a.Code, a.Name, a.Description))
            .ToListAsync();

        return Ok(catalog);
    }

    [HttpGet("staff/{staffId}")]
    public async Task<ActionResult<IEnumerable<StaffAuthorizationDto>>> GetStaffAuthorizations(Guid staffId)
    {
        var auths = await _context.StaffAuthorizations
            .Include(a => a.Authorization)
            .Include(a => a.GrantedByUser)
            .Where(a => a.StaffId == staffId)
            .OrderByDescending(a => a.GrantedAt)
            .Select(a => new StaffAuthorizationDto(
                a.Id,
                a.AuthorizationId,
                a.Authorization != null ? a.Authorization.Name : "Desconocido",
                a.Authorization != null ? a.Authorization.Description : null,
                a.ValidFrom,
                a.ValidUntil,
                a.GrantedAt,
                a.Status,
                a.GrantedByUser != null ? a.GrantedByUser.FullName : "Desconocido"
            ))
            .ToListAsync();

        return Ok(auths);
    }

    [HttpPost("grant")]
    public async Task<ActionResult> GrantAuthorization(GrantAuthorizationRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid grantedBy = userIdStr != null ? Guid.Parse(userIdStr) : Guid.Empty;

            // Find or create authorization catalog entry
            var authCatalog = await _context.AuthorizationCatalogs
                .FirstOrDefaultAsync(a => a.Name == request.TaskName);
            
            if (authCatalog == null)
            {
                authCatalog = new AuthorizationCatalog
                {
                    Id = Guid.NewGuid(),
                    Code = $"AUTH-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Name = request.TaskName,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuthorizationCatalogs.Add(authCatalog);
            }

            var validFrom = DateTime.SpecifyKind(request.ValidFrom, DateTimeKind.Utc);
            var validUntil = request.ValidUntil.HasValue 
                ? DateTime.SpecifyKind(request.ValidUntil.Value, DateTimeKind.Utc) 
                : (DateTime?)null;

            var auth = new StaffAuthorization
            {
                Id = Guid.NewGuid(),
                StaffId = request.StaffId,
                AuthorizationId = authCatalog.Id,
                GrantedByUserId = grantedBy,
                GrantedAt = DateTime.UtcNow,
                ValidFrom = validFrom,
                ValidUntil = validUntil,
                Status = "VIGENTE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffAuthorizations.Add(auth);
            await _context.SaveChangesAsync();

            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error granting authorization: {ex.Message}");
            return StatusCode(500, $"Error al emitir autorización: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAuthorization(Guid id)
    {
        try
        {
            var auth = await _context.StaffAuthorizations.FindAsync(id);
            if (auth == null) return NotFound();

            // Audit log
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"[AUDIT] User {userIdStr} deleted authorization {id} at {DateTime.UtcNow}");

            _context.StaffAuthorizations.Remove(auth);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting authorization: {ex.Message}");
            return StatusCode(500, $"Error al eliminar autorización: {ex.Message}");
        }
    }
}
