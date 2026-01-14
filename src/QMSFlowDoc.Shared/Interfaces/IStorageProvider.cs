using System;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Shared.Interfaces;

public interface IStorageProvider
{
    string ProviderName { get; }
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string mimeType);
    Task<Stream> DownloadFileAsync(string fileId);
    Task<bool> DeleteFileAsync(string fileId);
}

public record StorageConfig(
    string ProviderType,
    string ClientId,
    string ClientSecret,
    string? RootFolderId
);
