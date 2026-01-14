using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

public interface ISyncLogger
{
    Task LogAsync(string message, string level = "INFO", string? details = null);
    Task LogErrorAsync(string message, Exception? ex = null);
}

public class SyncLogger : ISyncLogger
{
    private readonly string _logPath;
    private readonly object _lock = new object();

    public SyncLogger()
    {
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc", "Logs");
        Directory.CreateDirectory(appFolder);
        // Rotate logs by day
        _logPath = Path.Combine(appFolder, $"sync_tech_{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public async Task LogAsync(string message, string level = "INFO", string? details = null)
    {
        var line = $"[{DateTime.UtcNow:O}] [{level}] {message}";
        if (!string.IsNullOrEmpty(details)) line += $" | Details: {details}";
        line += Environment.NewLine;

        await File.AppendAllTextAsync(_logPath, line, Encoding.UTF8);
    }

    public async Task LogErrorAsync(string message, Exception? ex = null)
    {
        var details = ex?.ToString();
        await LogAsync(message, "ERROR", details);
    }
}
