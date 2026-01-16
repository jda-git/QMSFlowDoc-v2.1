using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Windows.Storage;
using Windows.Storage.Pickers;
using QMSFlowDoc.Shared.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QMSFlowDoc.Client.Services;

public interface IExportService
{
    Task ExportReagentsToExcelAsync(IEnumerable<ReagentListDto> reagents);
    Task ExportEquipmentToExcelAsync(IEnumerable<EquipmentListDto> equipment);
    Task ExportStaffToExcelAsync(IEnumerable<StaffListDto> staff);
    Task ExportAuditLogToPdfAsync(IEnumerable<AuditLogDto> auditLogs);
    Task ExportEqaReportToPdfAsync(IEnumerable<EQAResultDto> eqaResults, string programName);
}

public class ExportService : IExportService
{
    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task ExportReagentsToExcelAsync(IEnumerable<ReagentListDto> reagents)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Inventario");

        // Headers
        ws.Cell(1, 1).Value = "Nombre";
        ws.Cell(1, 2).Value = "Código Interno";
        ws.Cell(1, 3).Value = "Fabricante";
        ws.Cell(1, 4).Value = "Referencia";
        ws.Cell(1, 5).Value = "Stock Total";
        ws.Cell(1, 6).Value = "Stock Mínimo";
        ws.Cell(1, 7).Value = "Stock Objetivo";
        ws.Cell(1, 8).Value = "Estado";
        ws.Row(1).Style.Font.Bold = true;

        int row = 2;
        foreach (var r in reagents)
        {
            ws.Cell(row, 1).Value = r.Name;
            ws.Cell(row, 2).Value = r.InternalCode;
            ws.Cell(row, 3).Value = r.Manufacturer;
            ws.Cell(row, 4).Value = r.Reference;
            ws.Cell(row, 5).Value = r.TotalStock;
            ws.Cell(row, 6).Value = r.MinStock;
            ws.Cell(row, 7).Value = r.TargetStock;
            ws.Cell(row, 8).Value = r.Status.ToString();
            row++;
        }

        ws.Columns().AdjustToContents();
        await SaveWorkbookAsync(workbook, "Inventario_Reactivos");
    }

    public async Task ExportEquipmentToExcelAsync(IEnumerable<EquipmentListDto> equipment)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Equipos");

        // Headers
        ws.Cell(1, 1).Value = "Etiqueta";
        ws.Cell(1, 2).Value = "Nombre";
        ws.Cell(1, 3).Value = "Modelo";
        ws.Cell(1, 4).Value = "Ubicación";
        ws.Cell(1, 5).Value = "Estado";
        ws.Cell(1, 6).Value = "Último Mantenimiento";
        ws.Cell(1, 7).Value = "Próximo Mantenimiento";
        ws.Row(1).Style.Font.Bold = true;

        int row = 2;
        foreach (var e in equipment)
        {
            ws.Cell(row, 1).Value = e.AssetTag ?? "";
            ws.Cell(row, 2).Value = e.Name;
            ws.Cell(row, 3).Value = e.Model ?? "";
            ws.Cell(row, 4).Value = e.Location ?? "";
            ws.Cell(row, 5).Value = e.Status.ToString();
            ws.Cell(row, 6).Value = e.LastMaintenanceAt?.ToString("d") ?? "-";
            ws.Cell(row, 7).Value = e.NextMaintenanceDue ?? "-";
            row++;
        }

        ws.Columns().AdjustToContents();
        await SaveWorkbookAsync(workbook, "Equipos");
    }


    public async Task ExportStaffToExcelAsync(IEnumerable<StaffListDto> staff)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Personal");

        // Headers
        ws.Cell(1, 1).Value = "Nombre Completo";
        ws.Cell(1, 2).Value = "Cargo";
        ws.Cell(1, 3).Value = "Departamento";
        ws.Cell(1, 4).Value = "Estado";
        ws.Cell(1, 5).Value = "Formaciones";
        ws.Cell(1, 6).Value = "Competencias";
        ws.Row(1).Style.Font.Bold = true;

        int row = 2;
        foreach (var s in staff)
        {
            ws.Cell(row, 1).Value = s.FullName;
            ws.Cell(row, 2).Value = s.PositionTitle;
            ws.Cell(row, 3).Value = s.Department;
            ws.Cell(row, 4).Value = s.IsActive ? "Activo" : "Inactivo";
            ws.Cell(row, 5).Value = s.TrainingCount;
            ws.Cell(row, 6).Value = s.CompetencyPassCount;
            row++;
        }

        ws.Columns().AdjustToContents();
        await SaveWorkbookAsync(workbook, "Personal");
    }

    public async Task ExportAuditLogToPdfAsync(IEnumerable<AuditLogDto> auditLogs)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Text("Reporte de Auditoría - QMSFlowDoc")
                    .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Fecha");
                        header.Cell().Element(CellStyle).Text("Acción");
                        header.Cell().Element(CellStyle).Text("Recurso");
                        header.Cell().Element(CellStyle).Text("Detalles");
                        header.Cell().Element(CellStyle).Text("Usuario");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                        }
                    });

                    foreach (var log in auditLogs)
                    {
                        table.Cell().Element(CellStyle).Text(log.FormattedTimestamp);
                        table.Cell().Element(CellStyle).Text(log.Action);
                        table.Cell().Element(CellStyle).Text(log.Resource);
                        table.Cell().Element(CellStyle).Text(log.Details);
                        table.Cell().Element(CellStyle).Text(log.UserName ?? "-");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5);
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                    });
            });
        });

        await SavePdfAsync(document, "ReporteAuditoria");
    }

    public async Task ExportEqaReportToPdfAsync(IEnumerable<EQAResultDto> eqaResults, string programName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Text($"Reporte de Desempeño EQA - {programName}")
                    .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                page.Content().PaddingVertical(1, Unit.Centimetre).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(); // Cycle
                        columns.RelativeColumn(); // Status
                        columns.RelativeColumn(); // Performance
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Ciclo");
                        header.Cell().Element(CellStyle).Text("Estado");
                        header.Cell().Element(CellStyle).Text("Desempeño");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                        }
                    });

                    foreach (var result in eqaResults)
                    {
                        table.Cell().Element(CellStyle).Text(result.CycleIdentifier);
                        table.Cell().Element(CellStyle).Text(result.Status);
                        table.Cell().Element(CellStyle).Text(result.Performance).FontColor(GetPerformanceColor(result.PerformanceColor));

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5);
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                    });
            });
        });

        await SavePdfAsync(document, "ReporteEQA");
    }

    private string GetPerformanceColor(string colorName)
    {
        return colorName?.ToLower() switch
        {
            "green" => Colors.Green.Medium.ToString(),
            "red" => Colors.Red.Medium.ToString(),
            "orange" => Colors.Orange.Medium.ToString(),
            _ => Colors.Black.ToString()
        };
    }

    private async Task SaveWorkbookAsync(XLWorkbook workbook, string suggestedName)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Excel", new List<string> { ".xlsx" });
        picker.SuggestedFileName = $"{suggestedName}_{DateTime.Now:yyyyMMdd}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            using var stream = await file.OpenStreamForWriteAsync();
            stream.SetLength(0); // Clear existing content
            workbook.SaveAs(stream);
        }
    }

    private async Task SavePdfAsync(Document document, string suggestedName)
    {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
        picker.SuggestedFileName = $"{suggestedName}_{DateTime.Now:yyyyMMdd}";

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            using var stream = await file.OpenStreamForWriteAsync();
            stream.SetLength(0);
            document.GeneratePdf(stream);
        }
    }
}

