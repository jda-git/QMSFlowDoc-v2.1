using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Client.Services.Documents;

/// <summary>
/// Resultado de una operación de impresión
/// </summary>
public class PrintResult
{
    public string CopyId { get; set; } = "";
    public DateTime PrintedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Entrada del log de impresiones
/// </summary>
public class PrintLogEntry
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string CopyId { get; set; } = "";
    public string PrintedBy { get; set; } = "";
    public DateTime PrintedAt { get; set; }
    public string WatermarkType { get; set; } = "";
}

/// <summary>
/// Servicio para controlar impresiones con Copy IDs (ISO 15189)
/// </summary>
public class PrintControlService
{
    private readonly PdfWatermarkService _watermarkService;
    private readonly Services.Sync.IAuditLogger _auditLogger;
    private readonly string _dbPath;

    public PrintControlService(PdfWatermarkService watermarkService, Services.Sync.IAuditLogger auditLogger)
    {
        _watermarkService = watermarkService;
        _auditLogger = auditLogger;

        var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localFolder, "QMSFlowDoc");
        Directory.CreateDirectory(appFolder);
        _dbPath = Path.Combine(appFolder, "print_log.db");
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS PrintLog (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NOT NULL,
                CopyId TEXT NOT NULL UNIQUE,
                PrintedBy TEXT NOT NULL,
                PrintedAt TEXT NOT NULL,
                WatermarkType TEXT NOT NULL
            );";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<byte[]> PreparePrintPdfAsync(Document document, string localFilePath)
    {
        var originalBytes = await File.ReadAllBytesAsync(localFilePath);
        var latestVersion = document.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
        var versionLabel = latestVersion != null ? latestVersion.VersionLabel : "v1.0";

        // ISO: Las impresiones siempre son NO CONTROLADAS (requieren verificación de vigencia)
        return await _watermarkService.PrepareForExportAsync(
            originalBytes,
            versionLabel,
            DateTime.Now);
    }

    /// <summary>
    /// Imprime documento con Copy ID y registra en audit trail
    /// </summary>
    public async Task<PrintResult> PrintWithCopyIdAsync(Document document, string localFilePath, int copies = 1)
    {
        try
        {
            var copyId = GenerateCopyId(document);
            var printedAt = DateTime.UtcNow;

            // Preparar PDF con watermark y Copy ID
            var printBytes = await PreparePrintPdfAsync(document, localFilePath);

            // Guardar temporalmente para imprimir
            var tempPath = Path.Combine(Path.GetTempPath(), $"Print_{copyId}.pdf");
            await File.WriteAllBytesAsync(tempPath, printBytes);

            // Abrir con visor para que usuario imprima
            // (en entorno real, podríamos enviar directamente a impresora)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true,
                Verb = "print" // Intenta abrir diálogo de impresión directamente
            });

            // Registrar en PrintLog
            await LogPrintEventAsync(copyId, document, Environment.UserName, printedAt, "Uncontrolled");

            // Registrar en AuditLogger
            await _auditLogger.LogEventAsync(
                "DOCUMENT_PRINTED",
                Environment.UserName,
                document.DocCode ?? "Unknown",
                $"CopyID: {copyId}, Watermark: Uncontrolled, Copies: {copies}");

            return new PrintResult
            {
                CopyId = copyId,
                PrintedAt = printedAt,
                Success = true
            };
        }
        catch (Exception ex)
        {
            await _auditLogger.LogEventAsync(
                "DOCUMENT_PRINT_FAILED",
                Environment.UserName,
                document.DocCode ?? "Unknown",
                $"Error: {ex.Message}");

            return new PrintResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                PrintedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Registra evento de impresión en base de datos
    /// </summary>
    private async Task LogPrintEventAsync(
        string copyId,
        Document document,
        string user,
        DateTime printedAt,
        string watermarkType)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO PrintLog (Id, DocumentId, CopyId, PrintedBy, PrintedAt, WatermarkType)
            VALUES ($id, $docId, $copyId, $user, $printedAt, $watermark)";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$docId", document.Id.ToString());
        command.Parameters.AddWithValue("$copyId", copyId);
        command.Parameters.AddWithValue("$user", user);
        command.Parameters.AddWithValue("$printedAt", printedAt.ToString("O"));
        command.Parameters.AddWithValue("$watermark", watermarkType);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Obtiene historial de impresiones de un documento
    /// </summary>
    public async Task<System.Collections.Generic.List<PrintLogEntry>> GetPrintHistoryAsync(Guid documentId)
    {
        var list = new System.Collections.Generic.List<PrintLogEntry>();
        
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM PrintLog WHERE DocumentId = $docId ORDER BY PrintedAt DESC";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$docId", documentId.ToString());

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PrintLogEntry
            {
                Id = Guid.Parse(reader.GetString(0)),
                DocumentId = Guid.Parse(reader.GetString(1)),
                CopyId = reader.GetString(2),
                PrintedBy = reader.GetString(3),
                PrintedAt = DateTime.Parse(reader.GetString(4)),
                WatermarkType = reader.GetString(5)
            });
        }

        return list;
    }

    private string GenerateCopyId(Document document)
    {
        var latestVersion = document.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
        var versionLabel = latestVersion != null ? latestVersion.VersionLabel : "v1.0";
        var shortGuid = Guid.NewGuid().ToString("N")[..8].ToUpper();
        return $"{document.DocCode}-{versionLabel}-{shortGuid}";
    }
}
