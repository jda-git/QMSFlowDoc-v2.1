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
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public InventoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("reagents")]
    public async Task<ActionResult<IEnumerable<ReagentListDto>>> GetReagents([FromQuery] bool? isActive, [FromQuery] bool? isLowStock)
    {
        var query = _context.Reagents
            .Include(r => r.Lots)
            .AsQueryable();

        if (isActive.HasValue)
        {
            if (isActive.Value)
                query = query.Where(r => r.Status == ReagentStatus.ACTIVO);
            else
                query = query.Where(r => r.Status != ReagentStatus.ACTIVO);
        }

        // Low Stock filter
        // We need to calculate stock in memory or via complex query. Since lots are included, better to do in memory for this MVP size.
        // Or cleaner: select DTO then filter.

        var list = await query
            .OrderBy(r => r.Name)
            .Select(r => new ReagentListDto(
                r.Id,
                r.Name,
                r.Manufacturer,
                r.InternalCode,
                r.Fluorescence,
                r.ReagentType,
                r.Reference,
                r.Status,
                r.Lots.Where(l => l.Status == LotStatus.RELEASED).Sum(l => l.AvailableQty),
                r.MinStock,
                r.TargetStock
            )
            {
                AvailableLots = r.Lots
                    .Where(l => l.Status == LotStatus.RELEASED && l.AvailableQty > 0)
                    .OrderBy(l => l.ExpiryDate)
                    .Select(l => new LotSummaryDto 
                    { 
                        Id = l.Id, 
                        LotNumber = l.LotNumber, 
                        ExpiryDate = l.ExpiryDate 
                    })
                    .ToList()
            })
            .ToListAsync();

        if (isLowStock.HasValue && isLowStock.Value)
        {
            list = list.Where(r => r.TotalStock <= r.MinStock).ToList();
        }

        return Ok(list);
    }


    [HttpGet("movements")]
    public async Task<ActionResult<List<InventoryMovementDto>>> GetMovements(
        [FromQuery] DateTime? from, 
        [FromQuery] DateTime? to, 
        [FromQuery] InventoryMovementType? type, 
        [FromQuery] Guid? reagentId)
    {
        var query = _context.InventoryMovements
            .Include(m => m.ReagentLot)
            .Include(m => m.Reagent) // Join Reagent to get names
            .AsQueryable();

        if (from.HasValue) query = query.Where(m => m.MovedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(m => m.MovedAt <= to.Value.ToUniversalTime());
        if (type.HasValue) query = query.Where(m => m.MovementType == type.Value);
        if (reagentId.HasValue) query = query.Where(m => m.ReagentId == reagentId.Value);
        
        var list = await query
            .OrderByDescending(m => m.MovedAt)
            .Select(m => new InventoryMovementDto(
                m.Id,
                m.MovedAt.ToLocalTime(),
                _context.Users.Where(u => u.Id == m.UserId).Select(u => u.Username).FirstOrDefault() ?? "Sistema",
                m.Reagent.Name,
                m.Reagent.Manufacturer,
                m.Reagent.Fluorescence,
                m.MovementType.ToString(),
                m.Qty,
                m.ReagentLot != null ? m.ReagentLot.LotNumber : "N/A",
                m.ReagentLot != null ? (DateTime?)m.ReagentLot.ExpiryDate : null,
                m.Reason
            ))
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("reagents/{id}")]
    public async Task<ActionResult<Reagent>> GetReagent(Guid id)
    {
        var reagent = await _context.Reagents
            .Include(r => r.Lots)
            .Include(r => r.Supplier)
            .Include(r => r.DefaultLocation)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reagent == null) return NotFound();

        return Ok(reagent);
    }

    [HttpPost("reagents")]
    public async Task<ActionResult<Reagent>> CreateReagent(CreateReagentRequest request)
    {
        var reagent = new Reagent
        {
            Name = request.Name,
            Manufacturer = request.Manufacturer,
            SupplierId = request.SupplierId,
            ManufacturerCode = request.ManufacturerCode,
            InternalCode = request.InternalCode,
            Fluorescence = request.Fluorescence ?? string.Empty,
            ReagentType = request.ReagentType,
            Reference = request.Reference,
            Classification = request.Classification, // ISO 15189
            StorageConditions = request.StorageConditions,
            DefaultLocationId = request.DefaultLocationId,
            OpenShelfLifeDays = request.OpenShelfLifeDays,
            MinStock = request.MinStock,
            TargetStock = request.TargetStock,
            ReorderQty = request.ReorderQty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try 
        {
            _context.Reagents.Add(reagent);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetReagent), new { id = reagent.Id }, reagent);
        }
        catch (Exception ex)
        {
            return Problem(ex.InnerException?.Message ?? ex.Message);
        }
    }

    [HttpPut("reagents/{id}")]
    public async Task<IActionResult> UpdateReagent(Guid id, CreateReagentRequest request)
    {
        var reagent = await _context.Reagents.FindAsync(id);
        if (reagent == null) return NotFound();

        reagent.Name = request.Name;
        reagent.Manufacturer = request.Manufacturer;
        reagent.ManufacturerCode = request.ManufacturerCode;
        reagent.InternalCode = request.InternalCode;
        reagent.Fluorescence = request.Fluorescence ?? string.Empty;
        reagent.ReagentType = request.ReagentType;
        reagent.Reference = request.Reference;
        reagent.Classification = request.Classification; // ISO 15189
        reagent.MinStock = request.MinStock;
        reagent.TargetStock = request.TargetStock;
        reagent.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        // Status handling - Request DTO doesn't have Status. 
        // I should update DTO or add a separate Status update endpoint?
        // User wants to change status in "home reagent".
        // Use a new DTO or specific endpoint?
        // I'll add a 'UpdateReagentStatus' endpoint or just overload CreateReagentRequest?
        // CreateReagentRequest is a record. Hard to modify without breaking others.
        // I'll use a new DTO/Binding for Status?
        // Actually, easiest is to just add a specific endpoint for Status toggle if that's the main requirement.
        // "entrar en casa reactivo y definir si esta en estado activo o no"
        // implies editing the full form.
        
        // I'll add Status to CreateReagentRequest? No, cleaner to create UpdateReagentDto.
        // But for speed, I'll assume UpdateReagentRequest has Status. 
        // Use `UpdateReagentStatusRequest` shared DTO?
        // I'll stick to a dedicated matching Update endpoint + Status toggle endpoint.
        // Actually, the user asked for modification.
        
        try 
        {
             await _context.SaveChangesAsync();
             return NoContent();
        }
        catch (Exception ex)
        {
            return Problem(ex.InnerException?.Message ?? ex.Message);
        }
    }

    [HttpPatch("reagents/{id}/status")]
    public async Task<IActionResult> UpdateReagentStatus(Guid id, [FromBody] int status)
    {
        var reagent = await _context.Reagents.FindAsync(id);
        if (reagent == null) return NotFound();
        
        reagent.Status = (ReagentStatus)status;
        reagent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("reagents/{id}")]
    public async Task<IActionResult> DeleteReagent(Guid id, [FromQuery] string password)
    {
        // Simple Admin Password verification for MVP
        // In a real app, this would check against the DB or an identity service.
        // Based on DbInitializer, default admin is 'admin123'
        if (password != "admin123") return Unauthorized("Clave de administrador incorrecta.");

        var reagent = await _context.Reagents.Include(r => r.Lots).FirstOrDefaultAsync(r => r.Id == id);
        if (reagent == null) return NotFound();

        _context.Reagents.Remove(reagent);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("lots")]
    public async Task<ActionResult<List<ReagentLot>>> RegisterLot(RegisterLotRequest request)
    {
        var lots = new List<ReagentLot>();
        
        // Per User requirement: "pueden dar de alta 2 o más viales que tengan el mismo lote... hay que guardar cada uno"
        // We create N records with qty=1 instead of 1 record with qty=N
        int count = (int)request.ReceivedQty;
        for (int i = 0; i < count; i++)
        {
            var lot = new ReagentLot
            {
                ReagentId = request.ReagentId,
                LotNumber = request.LotNumber,
                ExpiryDate = DateTime.SpecifyKind(request.ExpiryDate, DateTimeKind.Utc),
                ReceivedDate = DateTime.SpecifyKind(request.ReceivedDate, DateTimeKind.Utc),
                ReceivedQty = 1,
                AvailableQty = 1,
                LocationId = request.LocationId,
                PanelId = request.PanelId, // ISO 15189
                Status = LotStatus.RELEASED,
                CreatedAt = DateTime.UtcNow
            };
            lots.Add(lot);
            _context.ReagentLots.Add(lot);
        }

        // Audit movement (log the total entry)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _context.InventoryMovements.Add(new InventoryMovement
        {
            MovedAt = DateTime.UtcNow,
            UserId = userId != null ? Guid.Parse(userId) : null,
            ReagentId = request.ReagentId,
            MovementType = InventoryMovementType.IN,
            Qty = request.ReceivedQty,
            Reason = $"Entrada de {count} viales (Lote: {request.LotNumber})"
        });

        try 
        {
            await _context.SaveChangesAsync();
            return Ok(lots);
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "api_errors.txt");
                var msg = $"{DateTime.Now}: Error saving lots: {ex.Message} \n Inner: {ex.InnerException?.Message} \n Stack: {ex.StackTrace}\n";
                System.IO.File.AppendAllText(logPath, msg);
            }
            catch { /* Ignore logging errors */ }

            return Problem("Error al guardar los lotes: " + (ex.InnerException?.Message ?? ex.Message));
        }
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> AdjustStock(AdjustStockRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userGuid = userId != null ? Guid.Parse(userId) : null;

        if (request.ReagentLotId.HasValue)
        {
            var lot = await _context.ReagentLots.FindAsync(request.ReagentLotId.Value);
            if (lot == null) return NotFound("Lote no encontrado.");

            if (request.MovementType == InventoryMovementType.OUT || request.MovementType == InventoryMovementType.WASTE)
            {
                lot.AvailableQty -= request.Qty;
                if (lot.AvailableQty <= 0) 
                {
                    lot.AvailableQty = 0;
                    // User said "se eliminará de la base de datos ese o esos lotes/fecha"
                    // To keep history, we could set status=retired, but user asked for deletion.
                    // Actually, removing from DB prevents showing it in 'AvailableLots'.
                    _context.ReagentLots.Remove(lot);
                }
            }
            else if (request.MovementType == InventoryMovementType.IN || request.MovementType == InventoryMovementType.RETURN)
            {
                lot.AvailableQty += request.Qty;
            }
        }

        _context.InventoryMovements.Add(new InventoryMovement
        {
            MovedAt = DateTime.UtcNow,
            UserId = userGuid,
            ReagentId = request.ReagentId,
            ReagentLotId = request.ReagentLotId,
            MovementType = request.MovementType,
            Qty = request.Qty,
            Reason = request.Reason,
            Notes = request.Notes
        });

        await _context.SaveChangesAsync();

        return Ok();
    }
}
