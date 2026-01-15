using System;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IAuditLogger
{
    Task LogEquipmentActionAsync(string action, string equipmentName, string? details, string? userId, string userName);
}

public class AuditLogger : IAuditLogger
{
    private readonly IConfigurationService _configService;
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;
    private string? _basePath;

    public AuditLogger(IConfigurationService configService, NetworkConfigStore networkConfig)
    {
        _configService = configService;
        _networkConfig = networkConfig;
    }

    private async Task<string> GetLogDirectoryAsync()
    {
        if (_basePath == null)
        {
            // Try to get from settings
            var setting = await _configService.GetSettingAsync("DocumentStoragePath");
            if (setting != null && !string.IsNullOrEmpty(setting.Value))
            {
                _basePath = setting.Value;
            }
            else
            {
                // Fallback to LocalApplicationData
                _basePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QMSFlowDoc",
                    "Logs"
                );
            }
        }

        var equipmentLogPath = Path.Combine(_basePath, "EQUIPOS");
        Directory.CreateDirectory(equipmentLogPath);
        return equipmentLogPath;
    }

    public async Task LogEquipmentActionAsync(string action, string equipmentName, string? details, string? userId, string userName)
    {
        // 1. File Logging (Legacy/Backup)
        try
        {
            var logDir = await GetLogDirectoryAsync();
            var logFile = Path.Combine(logDir, $"equipment_audit_{DateTime.Now:yyyy-MM}.log");

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Usuario: {userName} (ID: {userId}) | Acción: {action} | Equipo: {equipmentName}";
            if (!string.IsNullOrEmpty(details))
            {
                logEntry += $" | Detalles: {details}";
            }
            logEntry += Environment.NewLine;

            await File.AppendAllTextAsync(logFile, logEntry);
        }
        catch (Exception ex)
        {
            // Log silently
            System.Diagnostics.Debug.WriteLine($"Audit log file error: {ex.Message}");
        }

        // 2. SQLite Logging (Primary Local)
        try
        {
            if (_localStore == null)
            {
                _localStore = new LocalDocumentStore(_networkConfig);
                await _localStore.InitializeAsync();
            }
            
            await _localStore.CreateAuditLogAsync(
                action, 
                $"EQUIPMENT: {equipmentName}", 
                details ?? "", 
                userId, // Pass as string? CreateAuditLogAsync takes string userId?
                userName
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audit log sqlite error: {ex.Message}");
        }
    }
}
