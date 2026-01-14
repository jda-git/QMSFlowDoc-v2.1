using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Client.Services.Documents;

/// <summary>
/// Servicio para renderizar/abrir documentos con marca de agua temporal
/// </summary>
public class DocumentRenderer
{
    private readonly PdfWatermarkService _watermarkService;
    private readonly Services.Sync.IAuditLogger _auditLogger;

    public DocumentRenderer(PdfWatermarkService watermarkService, Services.Sync.IAuditLogger auditLogger)
    {
        _watermarkService = watermarkService;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Abre documento con marca de agua temporal (ISO: CONTROLADO para pantalla)
    /// </summary>
    public async Task OpenWithWatermarkAsync(Document document, string localFilePath)
    {
        try
        {
            var originalBytes = await File.ReadAllBytesAsync(localFilePath);
            var version = document.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
            var versionLabel = version?.VersionLabel ?? "v1.0";

            // Usar el nuevo método para vista en pantalla
            var watermarkedBytes = await _watermarkService.PrepareForScreenViewAsync(
                originalBytes,
                document.DocCode,
                versionLabel,
                document.Status.ToString(),
                document.NextReviewDue);

            var tempPath = Path.Combine(Path.GetTempPath(), $"VIEW_{document.DocCode}_{Guid.NewGuid().ToString("N")[..8]}.pdf");
            await File.WriteAllBytesAsync(tempPath, watermarkedBytes);

            Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });

            await _auditLogger.LogEventAsync("SCREEN_VIEW", Environment.UserName, document.DocCode, $"Vista en pantalla con marca CONTROLADO. Versión: {versionLabel}");
        }
        catch (Exception ex)
        {
            await _auditLogger.LogEventAsync("SCREEN_VIEW_FAILED", Environment.UserName, document.DocCode, $"Error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Exporta documento con marca de agua (ISO: NO CONTROLADO para exportación)
    /// </summary>
    public async Task<byte[]> ExportWithWatermarkAsync(Document document, string localFilePath)
    {
        var originalBytes = await File.ReadAllBytesAsync(localFilePath);
        var version = document.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
        var versionLabel = version?.VersionLabel ?? "v1.0";

        // Usar el nuevo método para exportación (NO CONTROLADO)
        var watermarkedBytes = await _watermarkService.PrepareForExportAsync(
            originalBytes,
            versionLabel,
            DateTime.Now);

        await _auditLogger.LogEventAsync("DOCUMENT_EXPORT", Environment.UserName, document.DocCode, $"Exportación/Impresión con marca NO CONTROLADO. Versión: {versionLabel}");

        return watermarkedBytes;
    }

    /// <summary>
    /// Vista previa rápida (en memoria)
    /// </summary>
    public async Task ShowPreviewAsync(string filePath)
    {
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        await _auditLogger.LogEventAsync("QUICK_PREVIEW", Environment.UserName, Path.GetFileName(filePath), "Vista previa rápida sin marcas");
    }
}
