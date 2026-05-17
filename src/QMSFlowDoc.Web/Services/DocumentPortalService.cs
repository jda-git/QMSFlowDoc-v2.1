using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Data;
using QMSFlowDoc.DocumentStorage;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Web.Services;

public sealed record DashboardSummary(int Documents, int ApprovedDocuments, int Equipment, int OpenNonconformities, int PendingReviews);

public sealed record DocumentListItem(
    Guid Id,
    string DocCode,
    string Title,
    string Status,
    string? Area,
    string? Process,
    string? CurrentVersion,
    DateTime UpdatedAt,
    bool HasFile);

public sealed record StoredDocumentFile(Stream Stream, string FileName, string MimeType);

public interface IDocumentPortalService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DocumentListItem>> GetDocumentsAsync(CancellationToken ct = default);
    Task<Guid> CreateDraftDocumentAsync(string docCode, string title, string? area, string? process, IFormFile file, Guid? userId, CancellationToken ct = default);
    Task<StoredDocumentFile?> OpenCurrentFileAsync(Guid documentId, CancellationToken ct = default);
}

public sealed class DocumentPortalService(QmsFlowDocDbContext db, IDocumentStorageService storage) : IDocumentPortalService
{
    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var documents = await db.Documents.CountAsync(ct);
        var approvedDocuments = await db.Documents.CountAsync(d => d.Status == DocumentStatus.APPROVED, ct);
        var equipment = await db.Equipments.CountAsync(ct);
        var openNonconformities = await db.Nonconformities.CountAsync(n => n.Status != NCStatus.CLOSED, ct);
        var pendingReviews = await db.Documents.CountAsync(d => d.NextReviewDue != null && d.NextReviewDue <= today, ct);

        return new DashboardSummary(documents, approvedDocuments, equipment, openNonconformities, pendingReviews);
    }

    public async Task<IReadOnlyList<DocumentListItem>> GetDocumentsAsync(CancellationToken ct = default)
    {
        var documents = await db.Documents
            .AsNoTracking()
            .Include(d => d.Versions)
            .OrderBy(d => d.DocCode)
            .Take(500)
            .ToListAsync(ct);

        return documents.Select(d =>
        {
            var current = d.Versions
                .OrderByDescending(v => v.IsCurrent)
                .ThenByDescending(v => v.VersionMajor)
                .ThenByDescending(v => v.VersionMinor)
                .ThenByDescending(v => v.CreatedAt)
                .FirstOrDefault();

            return new DocumentListItem(
                d.Id,
                d.DocCode,
                d.Title,
                d.Status.ToString(),
                d.Area,
                d.Process,
                current?.VersionLabel,
                d.UpdatedAt,
                !string.IsNullOrWhiteSpace(current?.LocalFilePath));
        }).ToArray();
    }

    public async Task<Guid> CreateDraftDocumentAsync(string docCode, string title, string? area, string? process, IFormFile file, Guid? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(docCode))
            throw new ArgumentException("El código del documento es obligatorio.", nameof(docCode));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del documento es obligatorio.", nameof(title));

        if (file.Length <= 0)
            throw new ArgumentException("El archivo está vacío.", nameof(file));

        await using var stream = file.OpenReadStream();
        var savedFile = await storage.SaveFileAsync(stream, file.FileName, "Documentos/Borradores", ct);

        var document = new Document
        {
            Id = Guid.NewGuid(),
            DocCode = docCode.Trim(),
            Title = title.Trim(),
            Area = string.IsNullOrWhiteSpace(area) ? null : area.Trim(),
            Process = string.IsNullOrWhiteSpace(process) ? null : process.Trim(),
            Status = DocumentStatus.DRAFT,
            OwnerUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        document.Versions.Add(new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionMajor = 0,
            VersionMinor = 1,
            VersionLabel = "0.1",
            ChangeSummary = "Alta inicial desde portal web QMSFlowDoc v3.",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            FileName = file.FileName,
            MimeType = savedFile.MimeType,
            Sha256 = savedFile.Sha256Hash,
            LocalFilePath = savedFile.RelativePath,
            IsCurrent = true
        });

        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);
        return document.Id;
    }

    public async Task<StoredDocumentFile?> OpenCurrentFileAsync(Guid documentId, CancellationToken ct = default)
    {
        var version = await db.DocumentVersions
            .AsNoTracking()
            .Where(v => v.DocumentId == documentId && v.IsCurrent && v.LocalFilePath != null)
            .OrderByDescending(v => v.VersionMajor)
            .ThenByDescending(v => v.VersionMinor)
            .FirstOrDefaultAsync(ct);

        if (version?.LocalFilePath is null)
            return null;

        var stream = await storage.ReadFileAsync(version.LocalFilePath, ct);
        var fileName = string.IsNullOrWhiteSpace(version.FileName) ? Path.GetFileName(version.LocalFilePath) : version.FileName;
        var mimeType = string.IsNullOrWhiteSpace(version.MimeType) ? "application/octet-stream" : version.MimeType;
        return new StoredDocumentFile(stream, fileName, mimeType);
    }
}
