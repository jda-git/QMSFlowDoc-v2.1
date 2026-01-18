using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

public interface ISyncLogger
{
    Task LogAsync(string message, string level = "INFO", string? details = null);
    Task LogErrorAsync(string message, Exception? ex = null);
    void SetBasePath(string localBasePath);
}

public class SyncLogger : ISyncLogger
{
    private string _logPath;
    private string? _localBasePath;
    private readonly object _lock = new object();
    private const int MaxLogAgeDays = 30;
    
    // Get machine name for unique log files (sanitized for filenames)
    private static readonly string MachineName = GetSanitizedMachineName();

    public SyncLogger()
    {
        // Default to AppData until SetBasePath is called
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc", "Logs");
        Directory.CreateDirectory(appFolder);
        _logPath = Path.Combine(appFolder, GetLogFileName());
    }

    /// <summary>
    /// Sets the base path for logs. Should be called after configuration is loaded.
    /// Logs will be stored in {localBasePath}/Base_datos/Logs/
    /// </summary>
    public void SetBasePath(string localBasePath)
    {
        if (string.IsNullOrWhiteSpace(localBasePath)) return;
        
        _localBasePath = localBasePath;
        var logsFolder = Path.Combine(localBasePath, "Base_datos", "Logs");
        Directory.CreateDirectory(logsFolder);
        _logPath = Path.Combine(logsFolder, GetLogFileName());
        
        // Cleanup old logs
        CleanupOldLogs(logsFolder);
    }

    public async Task LogAsync(string message, string level = "INFO", string? details = null)
    {
        var line = $"[{DateTime.UtcNow:O}] [{level}] {message}";
        if (!string.IsNullOrEmpty(details)) line += $" | Details: {details}";
        line += Environment.NewLine;

        try
        {
            await File.AppendAllTextAsync(_logPath, line, Encoding.UTF8);
        }
        catch
        {
            // Silently fail if log can't be written (don't break app)
        }
    }

    public async Task LogErrorAsync(string message, Exception? ex = null)
    {
        var details = ex?.ToString();
        await LogAsync(message, "ERROR", details);
    }

    /// <summary>
    /// Gets the log filename with date and machine name.
    /// Format: sync_YYYYMMDD_MACHINENAME.log
    /// </summary>
    private static string GetLogFileName()
    {
        return $"sync_{DateTime.UtcNow:yyyyMMdd}_{MachineName}.log";
    }

    /// <summary>
    /// Gets the machine name, sanitized for use in filenames.
    /// </summary>
    private static string GetSanitizedMachineName()
    {
        try
        {
            var name = Environment.MachineName;
            // Remove invalid filename characters
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.ToUpperInvariant();
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    private void CleanupOldLogs(string logsFolder)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-MaxLogAgeDays);
            var oldLogs = Directory.GetFiles(logsFolder, "sync_*.log")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc < cutoffDate);

            foreach (var logFile in oldLogs)
            {
                try { logFile.Delete(); } catch { }
            }
        }
        catch
        {
            // Silently fail cleanup
        }
    }
}
