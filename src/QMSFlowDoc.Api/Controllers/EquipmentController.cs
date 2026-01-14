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
public class EquipmentController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EquipmentController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentListDto>>> GetEquipment()
    {
        var equipmentList = await _context.Equipment
            .AsNoTracking()
            .Include(e => e.MaintenanceEvents)
            .OrderBy(e => e.Name)
            .ToListAsync();

        var result = equipmentList.Select(e =>
        {
            var lastEvent = e.MaintenanceEvents
                .OrderByDescending(me => me.PerformedAt)
                .ThenByDescending(me => me.Id)
                .FirstOrDefault();

            string? nextMaint = null;
            if (lastEvent != null && lastEvent.NextMaintenanceMonth.HasValue && lastEvent.NextMaintenanceYear.HasValue)
            {
                try
                {
                    var monthName = new DateTime(lastEvent.NextMaintenanceYear.Value, lastEvent.NextMaintenanceMonth.Value, 1)
                        .ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
                    nextMaint = $"{monthName} {lastEvent.NextMaintenanceYear}";
                }
                catch
                {
                    nextMaint = $"{lastEvent.NextMaintenanceMonth}/{lastEvent.NextMaintenanceYear}";
                }
            }

            return new EquipmentListDto(
                e.Id,
                e.AssetTag,
                e.Name,
                e.Model,
                e.SoftwareVersion, // ISO 15189
                e.FirmwareVersion, // ISO 15189
                e.Location,
                e.Status,
                lastEvent?.Id,
                lastEvent?.PerformedAt,
                lastEvent?.EventType.ToString(),
                lastEvent?.Outcome,
                nextMaint
            );
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Equipment>> GetEquipment(Guid id)
    {
        var equipment = await _context.Equipment
            .Include(e => e.MaintenancePlans)
            .Include(e => e.MaintenanceEvents)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (equipment == null) return NotFound();

        return Ok(equipment);
    }

    [HttpPost]
    public async Task<ActionResult<Equipment>> CreateEquipment(CreateEquipmentRequest request)
    {
        var equipment = new Equipment
        {
            Id = Guid.NewGuid(),
            AssetTag = request.AssetTag,
            Name = request.Name,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            SoftwareVersion = request.SoftwareVersion, // ISO 15189
            FirmwareVersion = request.FirmwareVersion, // ISO 15189
            Location = request.Location,
            InstalledAt = request.InstalledAt,
            Status = EquipmentStatus.ACTIVE
        };

        _context.Equipment.Add(equipment);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEquipment), new { id = equipment.Id }, equipment);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEquipment(Guid id, UpdateEquipmentRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest("ID mismatch");
        }

        var equipment = await _context.Equipment.FindAsync(id);
        if (equipment == null)
        {
            return NotFound();
        }

        equipment.AssetTag = request.AssetTag;
        equipment.Name = request.Name;
        equipment.Manufacturer = request.Manufacturer;
        equipment.Model = request.Model;
        equipment.SerialNumber = request.SerialNumber;
        equipment.SoftwareVersion = request.SoftwareVersion; // ISO 15189
        equipment.FirmwareVersion = request.FirmwareVersion; // ISO 15189
        equipment.Location = request.Location;
        equipment.InstalledAt = request.InstalledAt;

        await _context.SaveChangesAsync();

        return Ok(equipment);
    }

    [HttpPost("maintenance")]
    public async Task<ActionResult<MaintenanceEvent>> RegisterMaintenance(RegisterMaintenanceRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? userId = null;
            if (Guid.TryParse(userIdStr, out var parsedGuid))
            {
                userId = parsedGuid;
            }
            
            var perfAt = request.PerformedAt ?? DateTime.UtcNow;
            if (perfAt.Kind != DateTimeKind.Utc)
            {
                perfAt = DateTime.SpecifyKind(perfAt, DateTimeKind.Utc);
            }

            var maintenanceEvent = new MaintenanceEvent
            {
                Id = Guid.NewGuid(),
                EquipmentId = request.EquipmentId,
                PlanId = request.PlanId,
                PerformedAt = perfAt,
                PerformedByUserId = userId,
                EventType = request.EventType,
                Outcome = request.Outcome,
                Notes = request.Notes,
                EvidenceDocId = request.EvidenceDocId,
                HasIssues = request.HasIssues,
                NextMaintenanceMonth = request.NextMaintenanceMonth,
                NextMaintenanceYear = request.NextMaintenanceYear
            };

            _context.MaintenanceEvents.Add(maintenanceEvent);

            // ISO 15189 Req 3.2: Critical Failure -> Auto NC
            if (request.HasIssues == true)
            {
                var equipment = await _context.Equipment.FindAsync(request.EquipmentId);
                var equipName = equipment?.Name ?? "Unknown Equipment";

                var nc = new Nonconformity
                {
                    Id = Guid.NewGuid(),
                    Title = $"Fallo detectado en Mantenimiento: {equipName}",
                    Description = $"Se reportaron problemas durante el mantenimiento del equipo {equipName}. Resultado: {request.Outcome}. Notas: {request.Notes}",
                    Severity = NCSeverity.HIGH, // Default to High/Critical for failures
                    Status = NCStatus.OPEN,
                    DetectedAt = DateTime.UtcNow,
                    DetectedByUserId = userId,
                    Origin = $"Equipo: {equipName} ({equipment?.AssetTag})",
                    ImpactPatient = false // To be assessed
                };
                _context.Nonconformities.Add(nc);
            }

            await _context.SaveChangesAsync();

            return Ok(maintenanceEvent);
        }
        catch (Exception ex)
        {
            // Log the error (can use Console for now as we don't have a logger injected)
            Console.WriteLine($"Error registering maintenance: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            
            return StatusCode(500, $"Error interno del servidor: {ex.Message}");
        }
    }

    [HttpGet("{id}/maintenance/last")]
    public async Task<ActionResult<MaintenanceEvent>> GetLastMaintenance(Guid id)
    {
        var lastEvent = await _context.MaintenanceEvents
            .Where(me => me.EquipmentId == id)
            .OrderByDescending(me => me.PerformedAt)
            .ThenByDescending(me => me.Id)
            .FirstOrDefaultAsync();

        if (lastEvent == null) return NotFound();
        return Ok(lastEvent);
    }

    [HttpPut("maintenance/{id}")]
    public async Task<IActionResult> UpdateMaintenance(Guid id, UpdateMaintenanceRequest request)
    {
        if (id != request.Id) return BadRequest("ID mismatch");

        try
        {
            var maintenanceEvent = await _context.MaintenanceEvents.FindAsync(id);
            if (maintenanceEvent == null) return NotFound();

            var perfAt = request.PerformedAt ?? DateTime.UtcNow;
            if (perfAt.Kind != DateTimeKind.Utc)
            {
                perfAt = DateTime.SpecifyKind(perfAt, DateTimeKind.Utc);
            }

            maintenanceEvent.PerformedAt = perfAt;
            maintenanceEvent.EventType = request.EventType;
            maintenanceEvent.Outcome = request.Outcome;
            maintenanceEvent.Notes = request.Notes;
            maintenanceEvent.HasIssues = request.HasIssues;
            maintenanceEvent.NextMaintenanceMonth = request.NextMaintenanceMonth;
            maintenanceEvent.NextMaintenanceYear = request.NextMaintenanceYear;

            await _context.SaveChangesAsync();
            return Ok(maintenanceEvent);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al actualizar mantenimiento: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEquipment(Guid id)
    {
        var equipment = await _context.Equipment
            .Include(e => e.MaintenanceEvents)
            .Include(e => e.MaintenancePlans)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (equipment == null) return NotFound();

        _context.Equipment.Remove(equipment);
        await _context.SaveChangesAsync();

        return Ok();
    }
}
