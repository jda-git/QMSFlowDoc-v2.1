using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Storage;

/// <summary>
/// Proveedor de almacenamiento para carpetas de red UNC/SMB
/// </summary>
public class NetworkStorageProvider : IStorageProvider
{
    private readonly string _basePath;
    private const int MaxRetries = 5;
    private const int InitialRetryDelayMs = 1000;

    public NetworkStorageProvider(string networkBasePath)
    {
        if (string.IsNullOrWhiteSpace(networkBasePath))
            throw new ArgumentException("Network base path cannot be empty", nameof(networkBasePath));
            
        _basePath = networkBasePath.TrimEnd('\\', '/');
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Verificar que la ruta existe
            if (!Directory.Exists(_basePath))
            {
                throw new DirectoryNotFoundException($"Network path not found: {_basePath}");
            }
                
            // Verificar permisos de lectura
            _ = Directory.GetFiles(_basePath);
            
            // Verificar permisos de escritura con archivo temporal
            var testFile = Path.Combine(_basePath, $".qms_test_{Guid.NewGuid()}.tmp");
            await File.WriteAllTextAsync(testFile, "QMSFlowDoc connection test");
            
            // Verificar que se puede leer
            var content = await File.ReadAllTextAsync(testFile);
            if (content != "QMSFlowDoc connection test")
                throw new IOException("Write/Read verification failed");
            
            // Limpiar archivo de prueba
            File.Delete(testFile);
            
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Permisos insuficientes para acceder a {_basePath}. " +
                "Contacte al administrador IT o mapee la unidad con credenciales.", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"Carpeta de red no encontrada: {_basePath}. " +
                "Verifique que la ruta es correcta y está accesible.", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Error de red al acceder a {_basePath}: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<FileMetadata>> ListFilesAsync(string relativePath = "", bool recursive = true)
    {
        var fullPath = GetFullPath(relativePath);
        
        if (!Directory.Exists(fullPath))
            return Enumerable.Empty<FileMetadata>();
        
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        try
        {
            var files = Directory.GetFiles(fullPath, "*", searchOption)
                .Where(f => !f.Contains("\\_Trash\\")) // Excluir archivos en trash
                .Select(f => CreateFileMetadata(f));
            
            return await Task.FromResult(files);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directorios sin acceso
            return Enumerable.Empty<FileMetadata>();
        }
    }

    public async Task<FileMetadata?> GetMetadataAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        
        if (!File.Exists(fullPath))
            return null;
        
        return await Task.FromResult(CreateFileMetadata(fullPath));
    }

    public async Task<bool> CopyFileAsync(string sourceFullPath, string destRelativePath, bool overwrite = false)
    {
        var destFullPath = GetFullPath(destRelativePath);
        
        // Crear directorio destino si no existe
        var destDir = Path.GetDirectoryName(destFullPath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        // Intentar copia con retry por si hay locks
        return await CopyWithRetryAsync(sourceFullPath, destFullPath, overwrite);
    }

    public async Task<bool> MoveFileAsync(string sourceRelativePath, string destRelativePath)
    {
        var sourceFullPath = GetFullPath(sourceRelativePath);
        var destFullPath = GetFullPath(destRelativePath);
        
        if (!File.Exists(sourceFullPath))
            return false;
        
        // Crear directorio destino si no existe
        var destDir = Path.GetDirectoryName(destFullPath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        try
        {
            File.Move(sourceFullPath, destFullPath, overwrite: false);
            return true;
        }
        catch (IOException ex) when (ex.Message.Contains("being used"))
        {
            // Archivo bloqueado, intentar retry
            await Task.Delay(1000);
            try
            {
                File.Move(sourceFullPath, destFullPath, overwrite: false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<bool> DeleteFileAsync(string relativePath, bool moveToTrash = true)
    {
        var fullPath = GetFullPath(relativePath);
        
        if (!File.Exists(fullPath))
            return false;
        
        if (moveToTrash)
        {
            // Mover a carpeta _Trash en lugar de eliminar
            var trashPath = Path.Combine(_basePath, "_Trash", "network", 
                DateTime.Now.ToString("yyyy-MM-dd"), 
                Path.GetFileName(fullPath));
            
            var trashDir = Path.GetDirectoryName(trashPath);
            if (!string.IsNullOrEmpty(trashDir))
            {
                Directory.CreateDirectory(trashDir);
            }
            
            try
            {
                File.Move(fullPath, trashPath, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }
        else
        {
            // Eliminación definitiva (no recomendado)
            try
            {
                File.Delete(fullPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<bool> FileExistsAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        return await Task.FromResult(File.Exists(fullPath));
    }

    public async Task<Stream> ReadFileAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}");
        
        var memoryStream = new MemoryStream();
        using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await fileStream.CopyToAsync(memoryStream);
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task<long> GetFileSizeAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        
        if (!File.Exists(fullPath))
            return 0;
        
        return await Task.FromResult(new FileInfo(fullPath).Length);
    }

    public async Task<DateTime> GetLastModifiedAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        
        if (!File.Exists(fullPath))
            return DateTime.MinValue;
        
        return await Task.FromResult(File.GetLastWriteTimeUtc(fullPath));
    }

    public async Task<bool> CreateDirectoryAsync(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        
        try
        {
            Directory.CreateDirectory(fullPath);
            return await Task.FromResult(true);
        }
        catch
        {
            return false;
        }
    }

    // Métodos privados auxiliares

    private string GetFullPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return _basePath;
        
        // Normalizar separadores
        relativePath = relativePath.Replace('/', '\\');
        
        return Path.Combine(_basePath, relativePath);
    }

    private FileMetadata CreateFileMetadata(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        var relativePath = Path.GetRelativePath(_basePath, fullPath);
        
        return new FileMetadata
        {
            FullPath = fullPath,
            RelativePath = relativePath,
            Size = fileInfo.Length,
            ModifiedTimeUtc = fileInfo.LastWriteTimeUtc,
            IsDirectory = false,
            FileGuid = GenerateFileGuid(fullPath)
        };
    }

    private string GenerateFileGuid(string fullPath)
    {
        // Generar GUID basado en path completo (para tracking de renames)
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fullPath.ToLowerInvariant()));
        return new Guid(hash).ToString();
    }

    private async Task<bool> CopyWithRetryAsync(string source, string dest, bool overwrite)
    {
        int retryCount = 0;
        int delayMs = InitialRetryDelayMs;
        
        while (retryCount < MaxRetries)
        {
            try
            {
                File.Copy(source, dest, overwrite);
                return true;
            }
            catch (IOException ex) when (ex.Message.Contains("being used") && retryCount < MaxRetries - 1)
            {
                // Archivo bloqueado, esperar y reintentar con exponential backoff
                retryCount++;
                await Task.Delay(delayMs);
                delayMs *= 2; // 1s, 2s, 4s, 8s, 16s
            }
            catch (IOException)
            {
                // Otro error de IO (disco lleno, etc.)
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        
        return false; // Máximo de reintentos alcanzado
    }
}
