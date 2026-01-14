using System;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IAuditLogger
{
    Task LogEquipmentActionAsync(string action, string equipmentName, string? details, string userName);
}

public class AuditLogger : IAuditLogger
{
    private readonly IConfigurationService _configService;
    private string? _basePath;

    public AuditLogger(IConfigurationService configService)
    {
        _configService = configService;
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

    public async Task LogEquipmentActionAsync(string action, string equipmentName, string? details, string userName)
    {
        try
        {
            var logDir = await GetLogDirectoryAsync();
            var logFile = Path.Combine(logDir, $"equipment_audit_{DateTime.Now:yyyy-MM}.log");

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Usuario: {userName} | Acción: {action} | Equipo: {equipmentName}";
            if (!string.IsNullOrEmpty(details))
            {
                logEntry += $" | Detalles: {details}";
            }
            logEntry += Environment.NewLine;

            await File.AppendAllTextAsync(logFile, logEntry);
        }
        catch (Exception ex)
        {
            // Log silently - don't fail operations because of logging issues
            System.Diagnostics.Debug.WriteLine($"Audit log error: {ex.Message}");
        }
    }
}
