using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace QMSFlowDoc.Client.Services.Sync;

public record RemoteFile
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string ParentId { get; init; } = "";
    public long? Size { get; init; }
    public DateTime? ModifiedTime { get; init; } // UTC
    public string Md5Checksum { get; init; } = "";
    public bool IsFolder { get; init; }
}

public interface IDriveStorageProvider
{
    Task ConnectAsync();
    Task<List<RemoteFile>> ListAllFilesAsync(string rootFolderId);
    Task<string> UploadFileAsync(string localPath, string parentId, string? existingId = null);
    Task DownloadFileAsync(string fileId, string localPath);
    Task<string> CreateFolderAsync(string name, string parentId);
    Task DeleteFileAsync(string fileId);
    Task MoveFileAsync(string fileId, string newParentId);
}

public class GoogleDriveProvider : IDriveStorageProvider
{
    private readonly string[] Scopes = { Google.Apis.Drive.v3.DriveService.Scope.DriveFile };
    private readonly string ApplicationName = "QMSFlowDoc";
    private DriveService? _driveService;

    public async Task ConnectAsync()
    {
         UserCredential credential;
         var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
         var appFolder = Path.Combine(localFolder, "QMSFlowDoc");
         
         // In production, client_secret.json should be embedded or loaded securely.
         // For now, checks if exists in app folder.
         var secretPath = Path.Combine(appFolder, "client_secret.json");
         if (!File.Exists(secretPath))
             throw new FileNotFoundException("client_secret.json not found in " + appFolder);

         using (var stream = new FileStream(secretPath, FileMode.Open, FileAccess.Read))
         {
             string credPath = Path.Combine(appFolder, "token");
             credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                 GoogleClientSecrets.FromStream(stream).Secrets,
                 Scopes,
                 "user",
                 CancellationToken.None,
                 new FileDataStore(credPath, true));
         }

         _driveService = new DriveService(new BaseClientService.Initializer()
         {
             HttpClientInitializer = credential,
             ApplicationName = ApplicationName,
         });
    }

    public async Task<List<RemoteFile>> ListAllFilesAsync(string rootFolderId)
    {
        var result = new List<RemoteFile>();
        var request = _driveService!.Files.List();
        // Query to get all children of rootFolderId recursively is complex.
        // Simplified strategy: Get ALL files impacting QMS. In reality, we might filter by mimetype or specific folder.
        // Here we assume we work within a specific folder ID or just list everything not trashed.
        
        request.Q = $"'{rootFolderId}' in parents and trashed = false";
        request.Fields = "nextPageToken, files(id, name, parents, size, modifiedTime, md5Checksum, mimeType)";
        request.PageSize = 1000;

        do
        {
            var response = await request.ExecuteAsync();
            foreach (var file in response.Files)
            {
                var parent = file.Parents != null && file.Parents.Count > 0 ? file.Parents[0] : "";
                result.Add(new RemoteFile
                {
                    Id = file.Id,
                    Name = file.Name,
                    ParentId = parent,
                    Size = file.Size,
                    ModifiedTime = file.ModifiedTimeDateTimeOffset?.UtcDateTime,
                    Md5Checksum = file.Md5Checksum,
                    IsFolder = file.MimeType == "application/vnd.google-apps.folder"
                });
            }
            request.PageToken = response.NextPageToken;
        } while (!string.IsNullOrEmpty(request.PageToken));

        return result;
    }

    public async Task<string> UploadFileAsync(string localPath, string parentId, string? existingId = null)
    {
        var fileName = Path.GetFileName(localPath);
        var mimeType = GetMimeType(fileName); // Helper needed or default

        using var uploadStream = new FileStream(localPath, FileMode.Open, FileAccess.Read);
        
        Google.Apis.Upload.IUploadProgress progress;
        string fileId = existingId ?? "";

        if (!string.IsNullOrEmpty(existingId))
        {
            var updateRequest = _driveService!.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingId, uploadStream, mimeType);
            progress = await updateRequest.UploadAsync();
        }
        else
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                Parents = new List<string> { parentId }
            };
            var createRequest = _driveService!.Files.Create(fileMetadata, uploadStream, mimeType);
            progress = await createRequest.UploadAsync();
            fileId = createRequest.ResponseBody?.Id ?? "";
        }

        if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
            throw new Exception("Upload failed: " + progress.Exception);

        return fileId;
    }

    public async Task DownloadFileAsync(string fileId, string localPath)
    {
        var request = _driveService!.Files.Get(fileId);
        using var stream = new FileStream(localPath, FileMode.Create);
        await request.DownloadAsync(stream);
    }
    
    public async Task<string> CreateFolderAsync(string name, string parentId)
    {
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = name,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new List<string> { parentId }
        };
        var request = _driveService!.Files.Create(fileMetadata);
        var file = await request.ExecuteAsync();
        return file.Id;
    }
    
    public async Task DeleteFileAsync(string fileId)
    {
        // Don't actually delete, move to trash logic or trash param?
        // _driveService.Files.Delete(fileId) permanently deletes.
        // Better to update 'trashed' = true.
        var body = new Google.Apis.Drive.v3.Data.File { Trashed = true };
        await _driveService!.Files.Update(body, fileId).ExecuteAsync();
    }
    
    public async Task MoveFileAsync(string fileId, string newParentId)
    {
        var file = await _driveService!.Files.Get(fileId).ExecuteAsync();
        var previousParents = string.Join(",", file.Parents);
        var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId);
        updateRequest.RemoveParents = previousParents;
        updateRequest.AddParents = newParentId;
        await updateRequest.ExecuteAsync();
    }

    private string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
