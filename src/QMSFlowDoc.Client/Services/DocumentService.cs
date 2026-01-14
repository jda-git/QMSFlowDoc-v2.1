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
    
    // Initialization
    Task InitializeAsync();
    
    // Audit Logging
    Task LogAsync(string action, string entityType, Guid? entityId, string details);
}

public class DocumentService : IDocumentService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalCacheService _cacheService;
    private readonly LocalDocumentStore? _localStore;
    private readonly NetworkConfigStore _networkConfig;
    private bool _useLocalMode = false;

    public DocumentService(HttpClient httpClient, ILocalCacheService cacheService, LocalDocumentStore? localStore = null)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _networkConfig = new NetworkConfigStore();
        _localStore = localStore;
        
        // Try to detect if API is available
        try
        {
            var testTask = _httpClient.GetAsync("health");
            testTask.Wait(TimeSpan.FromMilliseconds(500));
            _useLocalMode = !testTask.Result.IsSuccessStatusCode;
        }
        catch
        {
            // API not available - use local mode
            _useLocalMode = true;
        }
        
        // Initialize local store if needed and not provided (legacy fallback, but should rely on injection)
        if (_useLocalMode && _localStore == null)
        {
            _localStore = new LocalDocumentStore(_networkConfig);
        }
    }

    public async Task<IEnumerable<DocumentDto>> GetDocumentsAsync(bool includeObsolete = false)
    {
        if (_useLocalMode && _localStore != null)
        {
            var docs = await _localStore.GetAllDocumentsAsync(includeObsolete);
            var docDtos = new List<DocumentDto>();
            
            foreach (var d in docs)
            {
                string? typeName = null;
                if (d.DocumentTypeId.HasValue)
                {
                     var type = await _localStore.GetDocumentTypeByIdAsync(d.DocumentTypeId.Value);
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
            return await _cacheService.GetCachedDocumentsAsync();
        }
    }

    public async Task<IEnumerable<DocumentType>> GetDocumentTypesAsync()
    {
        if (_useLocalMode && _localStore != null)
        {
            return await _localStore.GetDocumentTypesAsync();
        }

        return await _httpClient.GetFromJsonAsync<IEnumerable<DocumentType>>("documents/types")
               ?? new List<DocumentType>();
    }

    public async Task<Document?> GetDocumentByIdAsync(Guid id)
    {
        Document? doc = null;
        if (_useLocalMode && _localStore != null)
        {
            doc = await _localStore.GetDocumentByIdAsync(id);
        }
        else
        {
            doc = await _httpClient.GetFromJsonAsync<Document>($"documents/{id}");
        }

        if (doc != null)
        {
            await LogAsync("VIEW", "Document", doc.Id, $"Vista de metadatos: {doc.Title} ({doc.DocCode})");
        }
        return doc;
    }

    public async Task<Document?> CreateDocumentAsync(CreateDocumentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("documents", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Document>();
        }
        return null;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, DocumentStatus newStatus, string comments)
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

    public async Task<bool> UpdateDocumentAsync(Guid id, CreateDocumentRequest request)
    {
        if (_useLocalMode && _localStore != null)
        {
            var success = await _localStore.UpdateDocumentAsync(id, request);
            if (success) await LogAsync("EDIT", "Document", id, $"Editado local: {request.Title}");
            return success;
        }

        var response = await _httpClient.PutAsJsonAsync($"documents/{id}", request);
        if (response.IsSuccessStatusCode)
        {
            await LogAsync("EDIT", "Document", id, $"Editado via API: {request.Title}");
            return true;
        }
        return false;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        if (_useLocalMode && _localStore != null)
        {
            var success = await _localStore.DeleteDocumentAsync(id);
            if (success) await LogAsync("TRASH", "Document", id, "Movido a papelera de auditoría");
            return success;
        }
        
        var response = await _httpClient.DeleteAsync($"documents/{id}");
        if (response.IsSuccessStatusCode)
        {
            await LogAsync("DELETE", "Document", id, "Borrado vía API");
            return true;
        }
        return false;
    }

    public async Task<bool> UploadFileAsync(Guid id, byte[] fileData, string fileName, string contentType)
    {
        if (_useLocalMode && _localStore != null)
        {
            // For local mode, we reuse CreateDocumentWithFileAsync logic which handles archival
            // We need to fetch the existing document metadata first
            var doc = await _localStore.GetDocumentByIdAsync(id);
            if (doc == null) return false;

            var folderName = "General";
            if (doc.FolderId.HasValue)
            {
                var folders = await _localStore.GetFoldersAsync();
                folderName = folders.FirstOrDefault(f => f.Id == doc.FolderId.Value)?.Name ?? "General";
            }

            var typeId = doc.DocumentTypeId;
            var versionLabel = doc.Versions?.FirstOrDefault(v => v.IsCurrent)?.VersionLabel ?? "v1.0";

            var result = await _localStore.CreateDocumentWithFileAsync(
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

    public async Task<byte[]?> GetFileContentAsync(Guid id)
    {
        if (_useLocalMode && _localStore != null)
        {
            var doc = await _localStore.GetDocumentByIdAsync(id);
            var version = doc?.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
            
            if (version?.LocalFilePath != null && System.IO.File.Exists(version.LocalFilePath))
            {
                await LogAsync("ACCESS", "Document", id, $"Archivo accedido localmente: {version.FileName}");
                return await System.IO.File.ReadAllBytesAsync(version.LocalFilePath);
            }
            return null;
        }

        try
        {
            var bytes = await _httpClient.GetByteArrayAsync($"documents/{id}/file");
            await LogAsync("DOWNLOAD", "Document", id, "Archivo descargado vía API");
            return bytes;
        }
        catch { return null; }
    }

    // New local mode method
    public async Task<Document?> CreateDocumentWithFileAsync(string docCode, string title, DocumentStatus status, Guid? documentTypeId, int? reviewIntervalMonths, string versionLabel, string? area, string? process, byte[] fileBytes, string fileName, string subFolderName)
    {
        if (_useLocalMode && _localStore != null)
        {
            var (document, version) = await _localStore.CreateDocumentWithFileAsync(docCode, title, status, documentTypeId, reviewIntervalMonths, versionLabel, area, process, fileBytes, fileName, subFolderName);
            // Attach version to document for return
            document.Versions = new List<DocumentVersion> { version };
            return document;
        }
        
        // Fallback to API mode (not implemented yet - would need separate endpoint)
        throw new NotImplementedException("API mode file upload not yet implemented");
    }

    public async Task InitializeAsync()
    {
        if (_localStore != null)
        {
            await _localStore.InitializeAsync();
        }
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId, string details)
    {
        if (_localStore != null)
        {
            await _localStore.LogAuditAsync(new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                UserId = Guid.Empty, // Placeholder
                UserName = "Usuario Local"
            });
        }
    }
}
