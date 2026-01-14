using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ConfigurationController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Reagent Types
    [HttpGet("reagent-types")]
    public async Task<ActionResult<IEnumerable<ReagentType>>> GetReagentTypes()
    {
        return await _context.ReagentTypes.ToListAsync();
    }

    [HttpPost("reagent-types")]
    public async Task<ActionResult<ReagentType>> CreateReagentType(ReagentType type)
    {
        type.Id = Guid.NewGuid();
        _context.ReagentTypes.Add(type);
        await _context.SaveChangesAsync();
        return Ok(type);
    }
    
    [HttpDelete("reagent-types/{id}")]
    public async Task<IActionResult> DeleteReagentType(Guid id)
    {
        var type = await _context.ReagentTypes.FindAsync(id);
        if (type == null) return NotFound();
        _context.ReagentTypes.Remove(type);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // System Settings
    [HttpGet("settings")]
    public async Task<ActionResult<IEnumerable<SystemSetting>>> GetSettings()
    {
        return await _context.SystemSettings.ToListAsync();
    }

    [HttpGet("settings/{key}")]
    public async Task<ActionResult<SystemSetting>> GetSetting(string key)
    {
        var setting = await _context.SystemSettings.FindAsync(key);
        if (setting == null) return NotFound();
        return Ok(setting);
    }

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] SystemSetting setting)
    {
        if (key != setting.Key) return BadRequest();

        var existing = await _context.SystemSettings.FindAsync(key);
        if (existing == null)
        {
            _context.SystemSettings.Add(setting);
        }
        else
        {
            existing.Value = setting.Value;
            existing.Description = setting.Description;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
