using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.DTOs;
using System.Collections.Generic;
using System.Linq;

namespace QMSFlowDoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SearchController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SearchResultDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<SearchResultDto>());

        var query = q.ToLower();
        var results = new List<SearchResultDto>();

        // Search Documents
        var docs = await _context.Documents
            .Where(d => d.Title.ToLower().Contains(query) || d.DocCode.ToLower().Contains(query))
            .Take(5)
            .Select(d => new SearchResultDto(d.Id, "DOCUMENT", d.Title, d.DocCode, "documents"))
            .ToListAsync();
        results.AddRange(docs);

        // Search Reagents
        var reagents = await _context.Reagents
            .Where(r => r.Name.ToLower().Contains(query) || r.InternalCode!.ToLower().Contains(query))
            .Take(5)
            .Select(r => new SearchResultDto(r.Id, "REAGENT", r.Name, r.InternalCode ?? "", "inventory"))
            .ToListAsync();
        results.AddRange(reagents);

        // Search Equipment
        var equipment = await _context.Equipment
            .Where(e => e.Name.ToLower().Contains(query) || e.AssetTag!.ToLower().Contains(query))
            .Take(5)
            .Select(e => new SearchResultDto(e.Id, "EQUIPMENT", e.Name, e.AssetTag ?? "", "equipment"))
            .ToListAsync();
        results.AddRange(equipment);

        return Ok(results);
    }
}
