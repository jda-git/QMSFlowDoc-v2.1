using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Storage;

/// <summary>
/// Metadata de archivo para abstracción de storage (Drive o Red)
/// </summary>
public record FileMetadata
{
    public string RelativePath { get; init; } = "";
    public string FullPath { get; init; } = "";
    public long Size { get; init; }
    public DateTime ModifiedTimeUtc { get; init; }
    public bool IsDirectory { get; init; }
    public string FileGuid { get; init; } = "";  // Para tracking de renames
    public string? Checksum { get; init; }  // MD5 o SHA-256
}

/// <summary>
/// Abstracción común para proveedores de almacenamiento (Drive, Red, etc.)
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Prueba la conexión al storage provider
    /// </summary>
    Task<bool> TestConnectionAsync();
    
    /// <summary>
    /// Lista todos los archivos en la ruta base de forma recursiva
    /// </summary>
    /// <param name="relativePath">Ruta relativa desde la base (ej: "Documentos/SOPs")</param>
    /// <param name="recursive">Si debe escanear subdirectorios</param>
    Task<IEnumerable<FileMetadata>> ListFilesAsync(string relativePath = "", bool recursive = true);
    
    /// <summary>
    /// Obtiene metadata de un archivo específico
    /// </summary>
    Task<FileMetadata?> GetMetadataAsync(string relativePath);
    
    /// <summary>
    /// Copia un archivo (upload o download según contexto)
    /// </summary>
    /// <param name="sourceFullPath">Ruta completa del archivo origen</param>
    /// <param name="destRelativePath">Ruta relativa del destino en este provider</param>
    /// <param name="overwrite">Si debe sobrescribir si existe</param>
    Task<bool> CopyFileAsync(string sourceFullPath, string destRelativePath, bool overwrite = false);
    
    /// <summary>
    /// Mueve/renombra un archivo dentro del storage
    /// </summary>
    Task<bool> MoveFileAsync(string sourceRelativePath, string destRelativePath);
    
    /// <summary>
    /// Elimina un archivo (puede mover a trash si moveToTrash=true)
    /// </summary>
    Task<bool> DeleteFileAsync(string relativePath, bool moveToTrash = true);
    
    /// <summary>
    /// Verifica si un archivo existe
    /// </summary>
    Task<bool> FileExistsAsync(string relativePath);
    
    /// <summary>
    /// Lee el contenido de un archivo como stream
    /// </summary>
    Task<Stream> ReadFileAsync(string relativePath);
    
    /// <summary>
    /// Obtiene el tamaño de un archivo en bytes
    /// </summary>
    Task<long> GetFileSizeAsync(string relativePath);
    
    /// <summary>
    /// Obtiene la fecha de última modificación UTC
    /// </summary>
    Task<DateTime> GetLastModifiedAsync(string relativePath);
    
    /// <summary>
    /// Crea un directorio si no existe
    /// </summary>
    Task<bool> CreateDirectoryAsync(string relativePath);
}
