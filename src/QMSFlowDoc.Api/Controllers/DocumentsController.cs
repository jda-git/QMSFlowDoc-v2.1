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
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DocumentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentDto>>> GetDocuments()
    {
        var documents = await _context.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Versions)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentDto(
                d.Id,
                d.DocCode,
                d.Title,
                d.DocumentType != null ? d.DocumentType.Name : "N/A",
                d.FolderId,
                d.Area,
                d.Process,
                d.Status,
                d.NextReviewDue,
                d.CreatedAt,
                d.Versions.OrderByDescending(v => v.CreatedAt).Select(v => v.VersionLabel).FirstOrDefault()
            ))
            .ToListAsync();

        return Ok(documents);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Document>> GetDocument(Guid id)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null) return NotFound();

        return Ok(document);
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<DocumentType>>> GetDocumentTypes()
    {
        return await _context.DocumentTypes.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Document>> CreateDocument(CreateDocumentRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = userIdString != null ? Guid.Parse(userIdString) : null;

        var document = new Document
        {
            DocCode = request.DocCode,
            Title = request.Title,
            DocumentTypeId = request.DocumentTypeId,
            FolderId = request.FolderId,
            Area = request.Area,
            Process = request.Process,
            OwnerUserId = userId,
            Status = request.Status ?? DocumentStatus.DRAFT,
            ReviewIntervalMonths = request.ReviewIntervalMonths,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.ReviewIntervalMonths.HasValue)
        {
            document.NextReviewDue = DateTime.UtcNow.AddMonths(request.ReviewIntervalMonths.Value);
        }

        // Add initial version if version label provided
        if (!string.IsNullOrWhiteSpace(request.VersionLabel))
        {
            document.Versions.Add(new DocumentVersion
            {
                VersionLabel = request.VersionLabel,
                VersionMajor = 1,
                IsCurrent = true,
                CreatedAt = DateTime.UtcNow,
                ChangeSummary = "Initial version"
            });
        }

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateDocument(Guid id, CreateDocumentRequest request)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id);
            
        if (document == null) return NotFound();

        // Check for code uniqueness if code changed
        if (request.DocCode != document.DocCode)
        {
            if (await _context.Documents.AnyAsync(d => d.DocCode == request.DocCode))
                 return BadRequest("Changes conflict with existing code.");
        }

        document.DocCode = request.DocCode;
        document.Title = request.Title;
        document.DocumentTypeId = request.DocumentTypeId;
        document.FolderId = request.FolderId;
        document.Area = request.Area;
        document.Process = request.Process;
        document.ReviewIntervalMonths = request.ReviewIntervalMonths;
        document.UpdatedAt = DateTime.UtcNow;

        if (request.Status.HasValue)
        {
            document.Status = request.Status.Value;
        }

        // Update current version label if provided
        if (!string.IsNullOrWhiteSpace(request.VersionLabel))
        {
            var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrent) 
                                ?? document.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
            if (currentVersion != null)
            {
                currentVersion.VersionLabel = request.VersionLabel;
            }
        }

        if (request.ReviewIntervalMonths.HasValue && document.Status != DocumentStatus.OBSOLETE)
        {
             // Recalculate next review? Simple logic for now
             if (document.NextReviewDue == null || document.NextReviewDue < DateTime.UtcNow)
                 document.NextReviewDue = DateTime.UtcNow.AddMonths(request.ReviewIntervalMonths.Value);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult> UpdateStatus(Guid id, UpdateDocumentStatusRequest request)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null) return NotFound();

        // Basic workflow validation
        // Draft -> In Review -> Approved -> Obsolete
        
        document.Status = request.NewStatus;
        document.UpdatedAt = DateTime.UtcNow;

        // ISO 15189 Req 1.1 Approval Workflow
        if (request.NewStatus == DocumentStatus.APPROVED)
        {
            var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrent) 
                                ?? document.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
            
            if (currentVersion != null)
            {
                currentVersion.ApprovedByUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                currentVersion.ApprovalDate = DateTime.UtcNow;
            }
        }

        // ISO 15189 Req 0.2 Audit Trail
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            UserName = User.Identity?.Name ?? "Unknown",
            Action = $"STATUS_CHANGED_TO_{request.NewStatus}",
            EntityType = "Document",
            EntityId = id,
            Details = $"Estado cambiado a {request.NewStatus}. Comentarios: {request.Comments}",
            Reason = request.Comments,
            MachineName = Environment.MachineName
        };
        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/upload")]
    public async Task<ActionResult> UploadFile(Guid id, IFormFile file)
    {
        var document = await _context.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == id);
        if (document == null) return NotFound();

        if (file == null || file.Length == 0) return BadRequest("No se ha proporcionado ningún archivo.");

        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "Documents");
        if (!Directory.Exists(storagePath)) Directory.CreateDirectory(storagePath);

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(storagePath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var version = new DocumentVersion
        {
            DocumentId = id,
            VersionMajor = document.Versions.Any() ? document.Versions.Max(v => v.VersionMajor) + 1 : 1,
            VersionMinor = 0,
            VersionLabel = document.Versions.Any() ? $"v{document.Versions.Max(v => v.VersionMajor) + 1}.0" : "v1.0",
            ChangeSummary = "Carga inicial de documento",
            FileName = file.FileName,
            MimeType = file.ContentType,
            LocalFilePath = fileName,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow
        };

        // Mark previous versions as not current
        foreach (var v in document.Versions) v.IsCurrent = false;

        document.Versions.Add(version);
        document.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { version.Id, version.VersionLabel, version.FileName });
    }

    [HttpGet("{id}/file")]
    public async Task<IActionResult> GetFile(Guid id)
    {
        var document = await _context.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == id);
        if (document == null) return NotFound();

        var currentVersion = document.Versions.FirstOrDefault(v => v.IsCurrent) 
                             ?? document.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault();

        if (currentVersion == null) return NotFound("No version found for this document.");

        if (string.IsNullOrEmpty(currentVersion.LocalFilePath)) return NotFound("No physical file path recorded for this version.");

        var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Storage", "Documents");
        var filePath = Path.Combine(storagePath, currentVersion.LocalFilePath);

        if (!System.IO.File.Exists(filePath)) return NotFound("Physical file not found.");

        var memory = new MemoryStream();
        using (var stream = new FileStream(filePath, FileMode.Open))
        {
            await stream.CopyToAsync(memory);
        }
        memory.Position = 0;

        return File(memory, currentVersion.MimeType ?? "application/octet-stream", currentVersion.FileName);
    }
}
