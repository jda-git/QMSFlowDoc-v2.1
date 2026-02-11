using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IDocumentService
{
    Task<IEnumerable<DocumentDto>> GetDocumentsAsync(bool includeObsolete = false);
    Task<IEnumerable<DocumentType>> GetDocumentTypesAsync();
    Task<Document?> GetDocumentByIdAsync(Guid id);
    Task<Document?> CreateDocumentAsync(CreateDocumentRequest request);
    Task<bool> UpdateStatusAsync(Guid id, DocumentStatus newStatus, string comments);
    Task<bool> UpdateDocumentAsync(Guid id, CreateDocumentRequest request);
    Task<bool> DeleteDocumentAsync(Guid id);
    Task<bool> UploadFileAsync(Guid id, byte[] fileData, string fileName, string contentType);
    Task<byte[]?> GetFileContentAsync(Guid id);
    
    // New local mode methods
    Task<Document?> CreateDocumentWithFileAsync(string docCode, string title, DocumentStatus status, Guid? documentTypeId, int? reviewIntervalMonths, string versionLabel, string? area, string? process, byte[] fileBytes, string fileName, string subFolderName);
    Task<Guid?> GetOrCreateFolderIdAsync(string folderName);
    
    // Initialization
    Task InitializeAsync();
    
    // Audit Logging
    Task LogAsync(string action, string entityType, Guid? entityId, string details);
    
    // Status
    bool IsLocalMode { get; }
}

public class DocumentService : IDocumentService
{
    // _httpClient kept for constructor compatibility but unused
    private readonly HttpClient _httpClient; 
    private readonly ILocalCacheService _cacheService;
    private readonly NetworkConfigStore _networkConfig;
    private readonly IAuthService? _authService;
    private LocalDocumentStore? _localStore;

    public DocumentService(HttpClient httpClient, ILocalCacheService cacheService, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null, IAuthService? authService = null)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _networkConfig = networkConfig ?? new NetworkConfigStore();
        _localStore = localStore;
        _authService = authService;
    }

    private async Task<LocalDocumentStore> GetLocalStoreAsync()
    {
        if (_localStore == null)
        {
            _localStore = new LocalDocumentStore(_networkConfig);
        }
        // Ensure initialized
        await _localStore.InitializeAsync();
        return _localStore;
    }
    
    public bool IsLocalMode => true; 

    public async Task<IEnumerable<DocumentDto>> GetDocumentsAsync(bool includeObsolete = false)
    {
        var store = await GetLocalStoreAsync();
        var docs = await store.GetAllDocumentsAsync(includeObsolete);
        var docDtos = new List<DocumentDto>();
        
        foreach (var d in docs)
        {
            string? typeName = null;
            if (d.DocumentTypeId.HasValue)
            {
                    var type = await store.GetDocumentTypeByIdAsync(d.DocumentTypeId.Value);
                    typeName = type?.Name;
            }

            docDtos.Add(new DocumentDto 
            { 
                Id = d.Id, 
                DocCode = d.DocCode, 
                Title = d.Title, 
                Status = d.Status,
                FolderId = d.FolderId,
                DocumentTypeId = d.DocumentTypeId,
                TypeName = typeName,
                Area = d.Area,
                Process = d.Process,
                OwnerUserId = d.OwnerUserId,
                ReviewIntervalMonths = d.ReviewIntervalMonths ?? 12,
                NextReviewDue = d.NextReviewDue,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                // Map version info if available
                CurrentVersionLabel = d.Versions?.FirstOrDefault(v => v.IsCurrent)?.VersionLabel ?? "v1.0"
            });
        }
        return docDtos;
    }

    public async Task<IEnumerable<DocumentType>> GetDocumentTypesAsync()
    {
        var store = await GetLocalStoreAsync();
        return await store.GetDocumentTypesAsync();
    }

    public async Task<Document?> GetDocumentByIdAsync(Guid id)
    {
        var store = await GetLocalStoreAsync();
        var doc = await store.GetDocumentByIdAsync(id);
        
        if (doc != null)
        {
            await LogAsync("VIEW", "Document", doc.Id, $"Vista de metadatos: {doc.Title} ({doc.DocCode})");
        }
        return doc;
    }

    public async Task<Document?> CreateDocumentAsync(CreateDocumentRequest request)
    {
        var store = await GetLocalStoreAsync();
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            DocCode = request.DocCode,
            Title = request.Title,
            DocumentTypeId = request.DocumentTypeId,
            FolderId = request.FolderId,
            Area = request.Area,
            Process = request.Process,
            Status = request.Status ?? DocumentStatus.DRAFT,
            ReviewIntervalMonths = request.ReviewIntervalMonths,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        return await store.CreateDocumentAsync(doc);
    }

    public async Task<bool> UpdateStatusAsync(Guid id, DocumentStatus newStatus, string comments)
    {
        var store = await GetLocalStoreAsync();
        var success = await store.UpdateDocumentStatusAsync(id, newStatus);
        if (success) await LogAsync("STATUS_CHANGE", "Document", id, $"Estado cambiado localmente a {newStatus}. Motivo: {comments}");
        return success;
    }

    public async Task<bool> UpdateDocumentAsync(Guid id, CreateDocumentRequest request)
    {
        var store = await GetLocalStoreAsync();
        var success = await store.UpdateDocumentAsync(id, request);
        if (success) await LogAsync("EDIT", "Document", id, $"Editado local: {request.Title}");
        return success;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        var store = await GetLocalStoreAsync();
        var success = await store.DeleteDocumentAsync(id);
        if (success) await LogAsync("TRASH", "Document", id, "Movido a papelera de auditoría");
        return success;
    }

    public async Task<bool> UploadFileAsync(Guid id, byte[] fileData, string fileName, string contentType)
    {
        // Reusing CreateDocumentWithFile logic for archival if needed
        // But primarily getting existing doc and appending version
        var store = await GetLocalStoreAsync();
        var doc = await store.GetDocumentByIdAsync(id);
        if (doc == null) return false;

        var folderName = "General";
        if (doc.FolderId.HasValue)
        {
            var folders = await store.GetFoldersAsync();
            folderName = folders.FirstOrDefault(f => f.Id == doc.FolderId.Value)?.Name ?? "General";
        }

        var typeId = doc.DocumentTypeId;
        var versionLabel = doc.Versions?.FirstOrDefault(v => v.IsCurrent)?.VersionLabel ?? "v1.0";

        // Create new version with file
        var result = await store.CreateDocumentWithFileAsync(
            doc.DocCode,
            doc.Title,
            doc.Status,
            typeId,
            doc.ReviewIntervalMonths,
            versionLabel,
            doc.Area,
            doc.Process,
            fileData,
            fileName,
            folderName);
            
        return result.Document != null;
    }

    public async Task<byte[]?> GetFileContentAsync(Guid id)
    {
        try 
        {
            var store = await GetLocalStoreAsync();
            var doc = await store.GetDocumentByIdAsync(id);
            
            if (doc == null) 
            {
                throw new InvalidOperationException($"Documento con ID {id} no encontrado en la base de datos.");
            }

            var version = doc.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
            
            if (version == null)
            {
                throw new InvalidOperationException($"El documento '{doc.Title}' no tiene versiones registradas.");
            }
            
            if (string.IsNullOrEmpty(version.LocalFilePath))
            {
                throw new InvalidOperationException($"El documento '{doc.Title}' no tiene una ruta de archivo asociada.");
            }
            
            var path = version.LocalFilePath;
            
            // A. Direct Access
            if (System.IO.File.Exists(path))
            {
                await LogAsync("ACCESS", "Document", id, $"Archivo accedido localmente: {version.FileName}");
                return await System.IO.File.ReadAllBytesAsync(path);
            }

            // B. Dynamic Resolution (Portable Mode)
            var config = await _networkConfig.LoadAsync();
            if (config != null && !string.IsNullOrEmpty(config.LocalBasePath))
            {
                // Check if path contains "Documentos"
                var idx = path.IndexOf("Documentos", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var relative = path.Substring(idx); 
                    // Clean leading separators
                    if (relative.StartsWith(Path.DirectorySeparatorChar) || relative.StartsWith(Path.AltDirectorySeparatorChar))
                        relative = relative.Substring(1);

                    var newPath = Path.Combine(config.LocalBasePath, relative);
                    if (System.IO.File.Exists(newPath))
                    {
                        await LogAsync("ACCESS", "Document", id, $"Archivo recuperado dinámicamente: {version.FileName}");
                        return await System.IO.File.ReadAllBytesAsync(newPath);
                    }
                }
            }
            
            // If we got here, file not found at any location
            throw new FileNotFoundException(
                $"Archivo no encontrado. Ruta original: {path}. " +
                $"Base configurada: {config?.LocalBasePath ?? "(no configurada)"}. " +
                $"¿Se movió o eliminó el archivo?"
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFileContentAsync Error: {ex}");
            throw; // Re-throw with detailed message
        }
    }

    public async Task<Document?> CreateDocumentWithFileAsync(string docCode, string title, DocumentStatus status, Guid? documentTypeId, int? reviewIntervalMonths, string versionLabel, string? area, string? process, byte[] fileBytes, string fileName, string subFolderName)
    {
        var store = await GetLocalStoreAsync();
        var (document, version) = await store.CreateDocumentWithFileAsync(docCode, title, status, documentTypeId, reviewIntervalMonths, versionLabel, area, process, fileBytes, fileName, subFolderName);
        if (document != null)
        {
            if (document.Versions == null) document.Versions = new List<DocumentVersion>();
            document.Versions.Add(version);
        }
        return document;
    }

    public async Task<Guid?> GetOrCreateFolderIdAsync(string folderName)
    {
        var store = await GetLocalStoreAsync();
        return await store.FindOrCreateFolderIdAsync(folderName);
    }

    public async Task InitializeAsync()
    {
        await GetLocalStoreAsync();
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId, string details)
    {
        var store = await GetLocalStoreAsync();
        await store.LogAuditAsync(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            UserId = _authService?.CurrentUserId ?? Guid.Empty,
            UserName = _authService?.CurrentUsername ?? "Usuario Local"
        });
    }
}
