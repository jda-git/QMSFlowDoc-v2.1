using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Documents;

/// <summary>
/// Tipos de marca de agua para cumplimiento ISO 15189
/// </summary>
public enum WatermarkType
{
    Controlled,      // "CONTROLADO" - verde
    Uncontrolled,    // "NO CONTROLADO" - gris
    Obsolete,        // "OBSOLETO" - rojo
    Draft            // "BORRADOR" - amarillo
}

/// <summary>
/// Servicio para añadir marcas de agua a documentos PDF (ISO 15189)
/// </summary>
public class PdfWatermarkService
{
    /// <summary>
    /// Añade marca de agua diagonal a todas las páginas del PDF
    /// </summary>
    /// <param name="pdfBytes">Bytes del PDF original</param>
    /// <param name="type">Tipo de marca de agua</param>
    /// <param name="metadata">Metadata adicional (opcional)</param>
    /// <returns>PDF con marca de agua aplicada</returns>
    public async Task<byte[]> AddWatermarkAsync(byte[] pdfBytes, WatermarkType type, string? metadata = null)
    {
        return await Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfBytes);
            using var outputStream = new MemoryStream();
            
            // Abrir PDF existente
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);
            
            // Aplicar watermark a cada página
            foreach (PdfPage page in document.Pages)
            {
                ApplyWatermarkToPage(page, type, metadata);
            }
            
            // Guardar a memoria
            document.Save(outputStream, false);
            return outputStream.ToArray();
        });
    }

    /// <summary>
    /// Genera vista previa con marca de agua (en memoria, no guarda archivo)
    /// </summary>
    public async Task<Stream> PreviewWithWatermarkAsync(string filePath, WatermarkType type)
    {
        var bytes = await File.ReadAllBytesAsync(filePath);
        var watermarkedBytes = await AddWatermarkAsync(bytes, type);
        return new MemoryStream(watermarkedBytes);
    }

    /// <summary>
    /// Añade Copy ID al pie de página de todas las páginas
    /// </summary>
    /// <param name="pdfBytes">Bytes del PDF</param>
    /// <param name="copyId">Identificador único de copia</param>
    /// <param name="printedAt">Timestamp de impresión</param>
    /// <param name="username">Usuario que imprime</param>
    /// <returns>PDF con Copy ID en footer</returns>
    public async Task<byte[]> AddCopyIdAsync(byte[] pdfBytes, string copyId, DateTime printedAt, string username)
    {
        return await Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfBytes);
            using var outputStream = new MemoryStream();
            
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);
            
            foreach (PdfPage page in document.Pages)
            {
                var footerText = $"Copy ID: {copyId} | Impreso por: {username} | Fecha: {printedAt:yyyy-MM-dd HH:mm}";
                AddFooterToPage(page, footerText, XBrushes.Black);
            }
            
            document.Save(outputStream, false);
            return outputStream.ToArray();
        });
    }

    /// <summary>
    /// Prepara el documento para previsualización en pantalla (ISO: CONTROLADO)
    /// </summary>
    public async Task<byte[]> PrepareForScreenViewAsync(
        byte[] pdfBytes, 
        string docId, 
        string version, 
        string status, 
        DateTime? nextReview)
    {
        return await Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfBytes);
            using var outputStream = new MemoryStream();
            
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);
            
            var footerText = $"ID: {docId} | Versión: {version} | Estado: {status} | Próxima Revisión: {nextReview?.ToString("yyyy-MM-dd") ?? "N/A"}";
            
            foreach (PdfPage page in document.Pages)
            {
                ApplyWatermarkToPage(page, WatermarkType.Controlled, null);
                AddFooterToPage(page, footerText, XBrushes.DarkGreen);
            }

            // Bloquear impresión para forzar uso de botón imprimir del software
            // Se requiere OwnerPassword para que Acrobat active la seguridad
            var security = document.SecuritySettings;
            security.OwnerPassword = Guid.NewGuid().ToString(); 
            security.PermitPrint = false;
            security.PermitFullQualityPrint = false;
            security.PermitModifyDocument = false;
            security.PermitExtractContent = false;
            security.PermitAnnotations = false;
            security.PermitAnnotations = false;
            
            document.Save(outputStream, false);
            return outputStream.ToArray();
        });
    }

    /// <summary>
    /// Prepara el documento para exportación/impresión (ISO: NO CONTROLADO)
    /// </summary>
    public async Task<byte[]> PrepareForExportAsync(
        byte[] pdfBytes, 
        string version, 
        DateTime printedAt)
    {
        return await Task.Run(() =>
        {
            using var inputStream = new MemoryStream(pdfBytes);
            using var outputStream = new MemoryStream();
            
            var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);
            
            var footerText = $"Versión: {version} | Fecha Impresión: {printedAt:yyyy-MM-dd HH:mm} | VERIFICAR VIGENCIA (Copia no controlada)";
            
            foreach (PdfPage page in document.Pages)
            {
                ApplyWatermarkToPage(page, WatermarkType.Uncontrolled, null);
                AddFooterToPage(page, footerText, XBrushes.DarkRed);
            }
            
            document.Save(outputStream, false);
            return outputStream.ToArray();
        });
    }

    // --- Private Methods ---

    private void ApplyWatermarkToPage(PdfPage page, WatermarkType type, string? metadata)
    {
        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Prepend);
        
        // Configurar texto y color según tipo
        var (text, color) = GetWatermarkConfig(type);
        
        // Font dinámico según tamaño de página
        var fontSize = Math.Min(page.Width.Point, page.Height.Point) * 0.15; // 15% del lado más pequeño
        var font = new XFont("Arial", fontSize, XFontStyleEx.Bold);
        
        // Crear brush con transparencia
        var brush = new XSolidBrush(XColor.FromArgb(51, color.R, color.G, color.B)); // 20% opacity = 51/255
        
        // Calcular posición central
        var centerX = page.Width.Point / 2;
        var centerY = page.Height.Point / 2;
        
        // Guardar estado y aplicar transformación
        var state = gfx.Save();
        gfx.TranslateTransform(centerX, centerY);
        gfx.RotateTransform(-45); // Diagonal
        
        // Medir texto para centrarlo
        var size = gfx.MeasureString(text, font);
        
        // Dibujar watermark
        gfx.DrawString(text, font, brush, -size.Width / 2, size.Height / 4);
        
        // Restaurar estado
        gfx.Restore(state);
        
        // Añadir metadata si existe
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            var metadataFont = new XFont("Arial", 8, XFontStyleEx.Regular);
            var metadataBrush = new XSolidBrush(XColor.FromArgb(102, 128, 128, 128)); // 40% opacity gris
            gfx.DrawString(metadata, metadataFont, metadataBrush, 
                new XRect(10, page.Height.Point - 30, page.Width.Point - 20, 20), 
                XStringFormats.TopLeft);
        }
    }

    private void AddFooterToPage(PdfPage page, string footerText, XBrush color)
    {
        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        
        var font = new XFont("Arial", 8, XFontStyleEx.Regular);
        var brush = color ?? XBrushes.Black;
        
        var rect = new XRect(0, page.Height.Point - 25, page.Width.Point, 20);
        gfx.DrawString(footerText, font, brush, rect, XStringFormats.BottomCenter);
    }

    private (string Text, XColor Color) GetWatermarkConfig(WatermarkType type)
    {
        return type switch
        {
            WatermarkType.Controlled => ("CONTROLADO", XColor.FromArgb(0, 128, 0)),        // Verde
            WatermarkType.Uncontrolled => ("NO CONTROLADO", XColor.FromArgb(128, 128, 128)), // Gris
            WatermarkType.Obsolete => ("OBSOLETO", XColor.FromArgb(255, 0, 0)),           // Rojo
            WatermarkType.Draft => ("BORRADOR", XColor.FromArgb(255, 165, 0)),            // Naranja
            _ => ("NO CONTROLADO", XColor.FromArgb(128, 128, 128))
        };
    }
}
