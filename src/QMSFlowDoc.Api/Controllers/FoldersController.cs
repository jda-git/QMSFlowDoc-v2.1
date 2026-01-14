using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FoldersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FolderDto>>> GetFolders([FromQuery] Guid? parentId)
    {
        var folders = await _context.Folders
            .Where(f => f.ParentFolderId == parentId)
            .Select(f => new FolderDto(
                f.Id,
                f.Name,
                f.ParentFolderId,
                f.SubFolders.Count,
                f.Documents.Count
            ))
            .ToListAsync();

        return Ok(folders);
    }

    [HttpPost]
    public async Task<ActionResult<Folder>> CreateFolder(string name, Guid? parentId)
    {
        var folder = new Folder
        {
            Name = name,
            ParentFolderId = parentId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetFolders), new { parentId = folder.ParentFolderId }, folder);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateFolder(Guid id, string name)
    {
        var folder = await _context.Folders.FindAsync(id);
        if (folder == null) return NotFound();

        folder.Name = name;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteFolder(Guid id)
    {
        var folder = await _context.Folders
            .Include(f => f.SubFolders)
            .Include(f => f.Documents)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder == null) return NotFound();

        if (folder.SubFolders.Any() || folder.Documents.Any())
        {
            return BadRequest("No se puede eliminar una carpeta que contiene subcarpetas o documentos.");
        }

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
