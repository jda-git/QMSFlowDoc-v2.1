using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

public interface IAuditLogger
{
    Task LogEventAsync(string eventType, string user, string resource, string description);
}

public class AuditLogger : IAuditLogger
{
    private readonly string _auditPath;

    public AuditLogger()
    {
        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc", "Logs");
        Directory.CreateDirectory(appFolder);
        // Audit log allows appending but should ideally be rotected. 
        // For MVP/Local, we use a CSV format for easy export/review.
        _auditPath = Path.Combine(appFolder, "audit_trail.csv");

        if (!File.Exists(_auditPath))
        {
            File.WriteAllText(_auditPath, "TimestampUTC,EventType,User,Resource,Description" + Environment.NewLine);
        }
    }

    public async Task LogEventAsync(string eventType, string user, string resource, string description)
    {
        // Simple CSV escaping
        var sanitizedDesc = description.Replace("\"", "\"\"");
        var line = $"{DateTime.UtcNow:O},{eventType},{user},{resource},\"{sanitizedDesc}\"{Environment.NewLine}";
        
        await File.AppendAllTextAsync(_auditPath, line, Encoding.UTF8);
    }
}
