using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
            await _localStore.InitializeAsync();
        }
        return _localStore;
    }
    
    public bool IsLocalMode => true; // Potentially dynamic but hybrid handles it

    private async Task CheckConnectionAsync()
    {
        // This method is no longer used in the new hybrid approach
        // The try-catch blocks in each method handle the connection status implicitly.
    }

    public async Task<IEnumerable<DocumentDto>> GetDocumentsAsync(bool includeObsolete = false)
    {
        try
        {
            var url = includeObsolete ? "documents?includeObsolete=true" : "documents";
            var documents = await _httpClient.GetFromJsonAsync<IEnumerable<DocumentDto>>(url) 
                           ?? new List<DocumentDto>();
            
            // Update cache in background
            _ = _cacheService.CacheDocumentsAsync(documents);
            
            return documents;
        }
        catch // Offline or Server Error
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
    }

    public async Task<IEnumerable<DocumentType>> GetDocumentTypesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<DocumentType>>("documents/types")
                   ?? new List<DocumentType>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetDocumentTypesAsync();
        }
    }

    public async Task<Document?> GetDocumentByIdAsync(Guid id)
    {
        Document? doc = null;
        try
        {
            doc = await _httpClient.GetFromJsonAsync<Document>($"documents/{id}");
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            doc = await store.GetDocumentByIdAsync(id);
        }

        if (doc != null)
        {
            await LogAsync("VIEW", "Document", doc.Id, $"Vista de metadatos: {doc.Title} ({doc.DocCode})");
        }
        return doc;
    }

    public async Task<Document?> CreateDocumentAsync(CreateDocumentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("documents", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Document>();
            }
            
            // Read error message from server for diagnostics
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Error del servidor ({(int)response.StatusCode}): {errorContent}");
        }
        catch
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
    }

    public async Task<bool> UpdateStatusAsync(Guid id, DocumentStatus newStatus, string comments)
    {
        try
        {
            var response = await _httpClient.PatchAsJsonAsync($"documents/{id}/status", 
                new { status = newStatus, comments });
            if (response.IsSuccessStatusCode)
            {
                await LogAsync("STATUS_CHANGE", "Document", id, $"Estado cambiado a {newStatus}. Motivo: {comments}");
                return true;
            }
            return false;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var success = await store.UpdateDocumentStatusAsync(id, newStatus);
            if (success) await LogAsync("STATUS_CHANGE", "Document", id, $"Estado cambiado localmente a {newStatus}. Motivo: {comments}");
            return success;
        }
    }

    public async Task<bool> UpdateDocumentAsync(Guid id, CreateDocumentRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"documents/{id}", request);
            if (response.IsSuccessStatusCode)
            {
                await LogAsync("EDIT", "Document", id, $"Editado via API: {request.Title}");
                return true;
            }
            return false;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var success = await store.UpdateDocumentAsync(id, request);
            if (success) await LogAsync("EDIT", "Document", id, $"Editado local: {request.Title}");
            return success;
        }
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"documents/{id}");
            if (response.IsSuccessStatusCode)
            {
                await LogAsync("DELETE", "Document", id, "Borrado vía API");
                return true;
            }
            return false;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var success = await store.DeleteDocumentAsync(id);
            if (success) await LogAsync("TRASH", "Document", id, "Movido a papelera de auditoría");
            return success;
        }
    }

    public async Task<bool> UploadFileAsync(Guid id, byte[] fileData, string fileName, string contentType)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileData);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync($"documents/{id}/upload", content);
            if (response.IsSuccessStatusCode)
            {
                await LogAsync("UPLOAD", "Document", id, $"Subido archivo: {fileName}");
                return true;
            }
            return false;
        }
        catch
        {
            // For local mode, we reuse CreateDocumentWithFileAsync logic which handles archival
            // We need to fetch the existing document metadata first
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
    }

    public async Task<byte[]?> GetFileContentAsync(Guid id)
    {
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync($"documents/{id}/file");
            await LogAsync("DOWNLOAD", "Document", id, "Archivo descargado vía API");
            return bytes;
        }
        catch 
        { 
            var store = await GetLocalStoreAsync();
            var doc = await store.GetDocumentByIdAsync(id);
            var version = doc?.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
            
            if (version?.LocalFilePath != null)
            {
                var path = version.LocalFilePath;
                
                // 1. Direct Access (Absolute Path is valid)
                if (System.IO.File.Exists(path))
                {
                    await LogAsync("ACCESS", "Document", id, $"Archivo accedido localmente: {version.FileName}");
                    return await System.IO.File.ReadAllBytesAsync(path);
                }

                // 2. Dynamic Resolution (Portable Mode)
                // If absolute path doesn't exist (e.g. moved PC), try to resolve relative to current Workspace
                try 
                {
                    var config = await _networkConfig.LoadAsync();
                    if (config != null && !string.IsNullOrEmpty(config.LocalBasePath))
                    {
                        // Strategy A: Check if path contains "Documentos" (standard structure)
                        var idx = path.IndexOf("Documentos", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var relative = path.Substring(idx); // Documentos\Subfolder\File.pdf
                            // Clean leading separators if any
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error resolving path dynamic: {ex.Message}");
                }
            }
            return null;
        }
    }

    // New local mode method
    public async Task<Document?> CreateDocumentWithFileAsync(string docCode, string title, DocumentStatus status, Guid? documentTypeId, int? reviewIntervalMonths, string versionLabel, string? area, string? process, byte[] fileBytes, string fileName, string subFolderName)
    {
        var store = await GetLocalStoreAsync();
        var (document, version) = await store.CreateDocumentWithFileAsync(docCode, title, status, documentTypeId, reviewIntervalMonths, versionLabel, area, process, fileBytes, fileName, subFolderName);
        if (document != null)
        {
            document.Versions = new List<DocumentVersion> { version };
        }
        return document;
    }

    public async Task<Guid?> GetOrCreateFolderIdAsync(string folderName)
    {
        try
        {
            // 1. List Root Folders
            var folders = await _httpClient.GetFromJsonAsync<IEnumerable<FolderDto>>("folders");
            var existing = folders?.FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null) return existing.Id;

            // 2. Create if not exists
            var response = await _httpClient.PostAsync($"folders?name={Uri.EscapeDataString(folderName)}", null);
            if (response.IsSuccessStatusCode)
            {
                var newFolder = await response.Content.ReadFromJsonAsync<Folder>();
                return newFolder?.Id;
            }
        }
        catch
        {
            // Fallback to local
        }

        var store = await GetLocalStoreAsync();
        return await store.FindOrCreateFolderIdAsync(folderName);
    }

    public async Task InitializeAsync()
    {
        await GetLocalStoreAsync();
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId, string details)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("audit/logs", new { action, entityType, entityId, details });
        }
        catch
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
}
