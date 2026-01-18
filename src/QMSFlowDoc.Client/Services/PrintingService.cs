using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace QMSFlowDoc.Client.Services;

public interface IPrintingService
{
    void GenerateControlledPdf(Shared.Models.Document document, Shared.Models.DocumentVersion version, string outputPath, string userName);
    void GenerateInventoryReport(IEnumerable<ReagentListDto> reagents, string sortInfo, string outputPath, string userName);
    void GenerateOrderReport(IEnumerable<ReagentListDto> items, string outputPath, string userName);
    void GenerateMovementsReport(IEnumerable<InventoryMovementDto> movements, string title, DateTime? start, DateTime? end, string outputPath, string userName);
    void GenerateEntriesReport(IEnumerable<InventoryMovementDto> movements, DateTime? start, DateTime? end, string outputPath, string userName);
    void GenerateTrainingPlan(StaffProfile profile, List<CompetencyEvaluationDto> competencies, List<StaffAuthorizationDto> authorizations, string outputPath, string userName);
}



public class PrintingService : IPrintingService
{
    public void GenerateControlledPdf(Shared.Models.Document document, Shared.Models.DocumentVersion version, string outputPath, string userName)
    {
        // QuestPDF License - Community (Required to be set)
        QuestPDF.Settings.License = LicenseType.Community;

        var documentModel = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("QMS FlowDoc - Documento Controlado").FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);
                        col.Item().Text($"{document.Title}").FontSize(12).SemiBold();
                    });

                    row.ConstantItem(100).Column(col =>
                    {
                        col.Item().Text($"Código: {document.DocCode}");
                        col.Item().Text($"Versión: {version.VersionLabel}");
                    });
                });

                // Content (Placeholder for actual document content)
                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text("Contenido del documento...").Italic();
                    
                    // Watermark (Diagonal)
                    col.Item().AlignCenter().Rotate(-45).Text(document.Status == Shared.Models.DocumentStatus.APPROVED ? "CONTROLADO" : "BORRADOR / NO CONTROLADO")
                        .FontSize(60).SemiBold().FontColor(Colors.Grey.Lighten3);
                });

                // Footer
                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(x =>
                        {
                            x.Span("Impreso por: ");
                            x.Span(userName).SemiBold();
                            x.Span($" el {DateTime.Now:dd/MM/yyyy HH:mm}");
                        });

                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Página ");
                            x.CurrentPageNumber();
                            x.Span(" de ");
                            x.TotalPages();
                        });
                    });
                    col.Item().Text($"ID Copia: {Guid.NewGuid().ToString().Substring(0, 8)}").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        documentModel.GeneratePdf(outputPath);
    }

    public void GenerateInventoryReport(IEnumerable<ReagentListDto> reagents, string sortInfo, string outputPath, string userName)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var doc = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Verdana));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Inventario de Reactivos").FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                        col.Item().Text($"Ordenado por: {sortInfo}").FontSize(10).Italic();
                    });
                    row.ConstantItem(150).AlignRight().Text($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}");
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // Nombre
                        columns.RelativeColumn(1.5f); // Fluor
                        columns.RelativeColumn(1.5f); // Codigo
                        columns.RelativeColumn(3); // Lotes
                        columns.RelativeColumn(2); // Fab
                        columns.RelativeColumn(1.5f); // Ref
                        columns.RelativeColumn(0.8f); // Stock
                        columns.RelativeColumn(0.8f); // Min
                        columns.RelativeColumn(1.2f); // Estado
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Nombre");
                        header.Cell().Element(CellStyle).Text("Fluor.");
                        header.Cell().Element(CellStyle).Text("Código");
                        header.Cell().Element(CellStyle).Text("Lotes Disponibles");
                        header.Cell().Element(CellStyle).Text("Fabricante");
                        header.Cell().Element(CellStyle).Text("Ref.");
                        header.Cell().Element(CellStyle).Text("Stock");
                        header.Cell().Element(CellStyle).Text("Mín.");
                        header.Cell().Element(CellStyle).Text("Estado");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                        }
                    });

                    foreach (var item in reagents)
                    {
                        table.Cell().Element(CellStyle).Text(item.Name);
                        table.Cell().Element(CellStyle).Text(item.Fluorescence ?? "-");
                        table.Cell().Element(CellStyle).Text(item.InternalCode ?? "-");
                        table.Cell().Element(CellStyle).Text(item.LotFechaSummary).FontSize(8);
                        table.Cell().Element(CellStyle).Text(item.Manufacturer ?? "-");
                        table.Cell().Element(CellStyle).Text(item.Reference ?? "-");
                        table.Cell().Element(CellStyle).Text($"{item.TotalStock}");
                        table.Cell().Element(CellStyle).Text($"{item.MinStock}");
                        table.Cell().Element(CellStyle).Text($"{item.Status}");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).PaddingVertical(2);
                        }
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Generado por: {userName}");
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        });
        doc.GeneratePdf(outputPath);
    }

    public void GenerateOrderReport(IEnumerable<ReagentListDto> items, string outputPath, string userName)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                page.Header().Column(col => 
                {
                   col.Item().AlignCenter().Text("Pedido de Reposición").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                   col.Item().AlignRight().Text($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}");
                });

                page.Content().PaddingVertical(20).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // Nombre
                        columns.RelativeColumn(1.5f); // Fluor
                        columns.RelativeColumn(1.5f); // Codigo Int
                        columns.RelativeColumn(2); // Fabricante
                        columns.RelativeColumn(1.5f); // Ref
                        columns.RelativeColumn(1); // Actual
                        columns.RelativeColumn(1); // Min
                        columns.RelativeColumn(1); // PEDIR
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HStyle).Text("Nombre");
                        header.Cell().Element(HStyle).Text("Fluorescencia");
                        header.Cell().Element(HStyle).Text("Cód. Interno");
                        header.Cell().Element(HStyle).Text("Fabricante");
                        header.Cell().Element(HStyle).Text("Referencia");
                        header.Cell().Element(HStyle).AlignRight().Text("Stock Actual");
                        header.Cell().Element(HStyle).AlignRight().Text("Mínimo");
                        header.Cell().Element(HStyle).AlignRight().Text("PEDIR");
                        
                        static IContainer HStyle(IContainer c) => c.BorderBottom(1).Padding(5).DefaultTextStyle(x => x.SemiBold());
                    });

                    foreach (var item in items)
                    {
                        var quantityToOrder = item.TargetStock - item.TotalStock;
                        
                        table.Cell().Element(CellStyle).Text(item.Name);
                        table.Cell().Element(CellStyle).Text(item.Fluorescence ?? "-");
                        table.Cell().Element(CellStyle).Text(item.InternalCode ?? "-");
                        table.Cell().Element(CellStyle).Text(item.Manufacturer ?? "-");
                        table.Cell().Element(CellStyle).Text(item.Reference ?? "-");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{item.TotalStock}");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{item.MinStock}");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{quantityToOrder}").Bold();

                        static IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5);
                    }
                });

                page.Footer().AlignCenter().Text($"Solicitado por: {userName}");
            });
        }).GeneratePdf(outputPath);
    }

    public void GenerateMovementsReport(IEnumerable<InventoryMovementDto> movements, string title, DateTime? start, DateTime? end, string outputPath, string userName)
    {
         // Keep existing implementation for Consumption/Movements generic
         QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Verdana));

                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                    col.Item().Text($"Periodo: {start:dd/MM/yyyy} - {end:dd/MM/yyyy}").FontSize(10);
                });

                page.Content().PaddingVertical(15).Column(stack =>
                {
                    var groups = movements.GroupBy(m => m.ReagentName).OrderBy(g => g.Key);

                    foreach (var group in groups)
                    {
                        var total = group.Sum(x => x.Qty);
                        var first = group.First();
                        
                        stack.Item().Column(c => 
                        {
                            c.Item().Background(Colors.Grey.Lighten4).Padding(5).Row(row => 
                            {
                                row.RelativeItem().Text(x => 
                                {
                                    x.Span($"{group.Key}").Bold().FontSize(11);
                                    if(!string.IsNullOrEmpty(first.Fluorescence)) x.Span($" ({first.Fluorescence})").Italic();
                                });
                                row.ConstantItem(150).AlignRight().Text($"Total Viales: {total}").Bold();
                            });

                            c.Item().PaddingLeft(10).Table(table => 
                            {
                                table.ColumnsDefinition(cols => 
                                {
                                    cols.ConstantColumn(80); // Date
                                    cols.RelativeColumn();   // Reason
                                    cols.ConstantColumn(80); // Lot
                                    cols.ConstantColumn(80); // Expiry
                                    cols.ConstantColumn(60); // Qty
                                });
                                
                                foreach(var move in group.OrderBy(x => x.MovedAt))
                                {
                                    table.Cell().Padding(2).Text($"{move.MovedAt:dd/MM/yy}");
                                    table.Cell().Padding(2).Text($"{move.Reason} ({move.UserName})");
                                    table.Cell().Padding(2).Text($"Lote: {move.LotNumber}");
                                    var expiry = move.ExpiryDate.HasValue ? move.ExpiryDate.Value.ToString("MM/yy") : "-";
                                    table.Cell().Padding(2).Text($"Cad: {expiry}");
                                    table.Cell().Padding(2).AlignRight().Text($"{move.Qty}"); 
                                }
                            });
                            c.Spacing(10);
                        });
                    }
                });

                page.Footer().Row(r => 
                {
                    r.RelativeItem().Text($"Generado el {DateTime.Now}");
                    r.RelativeItem().AlignRight().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            });
        }).GeneratePdf(outputPath);
    }

    public void GenerateEntriesReport(IEnumerable<InventoryMovementDto> movements, DateTime? start, DateTime? end, string outputPath, string userName)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                page.Header().Column(col =>
                {
                    col.Item().Text("Informe de Entradas").FontSize(18).SemiBold().FontColor(Colors.Blue.Medium);
                     col.Item().Text($"Periodo: {start:dd/MM/yyyy} - {end:dd/MM/yyyy}").FontSize(10);
                });

                page.Content().PaddingVertical(10).Column(stack =>
                {
                    // Group by Name
                    var groups = movements.GroupBy(m => m.ReagentName).OrderBy(g => g.Key);

                    foreach (var group in groups)
                    {
                        var totalParams = group.First();
                        var totalQty = group.Sum(x => x.Qty);

                        stack.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).Column(c => 
                        {
                            // Header Row: # Vials, Name, Fluor, Manufacturer
                            c.Item().Background(Colors.Grey.Lighten5).Padding(5).Row(row => 
                            {
                                row.ConstantItem(60).AlignRight().Text($"{totalQty}").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                                row.ConstantItem(10); // Spacer
                                row.RelativeItem(3).Text(totalParams.ReagentName).Bold().FontSize(12);
                                row.RelativeItem(2).Text(totalParams.Fluorescence ?? "-").Italic();
                                row.RelativeItem(2).Text(totalParams.Manufacturer ?? "-");
                            });

                            // Details Table: Lot - Expiry (Date from Lot)
                            // "listados de LOTE-FECHA recibidos entre esas fecha de inicio y fin"
                            c.Item().PaddingLeft(70).Table(table => 
                            {
                                table.ColumnsDefinition(cols => 
                                {
                                    cols.ConstantColumn(140); // Lot
                                    cols.ConstantColumn(120); // Expiry
                                    cols.ConstantColumn(120); // Entry Date
                                    cols.ConstantColumn(80);  // Units
                                });
                                
                                table.Header(h => 
                                {
                                    h.Cell().Text("Lote").SemiBold().FontSize(9);
                                    h.Cell().Text("Caducidad").SemiBold().FontSize(9);
                                    h.Cell().Text("Fecha Entrada").SemiBold().FontSize(9);
                                    h.Cell().AlignRight().Text("Unidades").SemiBold().FontSize(9);
                                });

                                foreach(var move in group.OrderBy(x => x.MovedAt))
                                {
                                    table.Cell().Text(move.LotNumber).FontSize(9);
                                    // Expiry might be null if not populated, assuming we fixed DTO
                                    var expiry = move.ExpiryDate.HasValue ? move.ExpiryDate.Value.ToString("MM/yy") : "-";
                                    table.Cell().Text(expiry).FontSize(9);
                                    table.Cell().Text($"{move.MovedAt:dd/MM/yyyy}").FontSize(9);
                                    table.Cell().AlignRight().Text($"{move.Qty}").FontSize(9);
                                }
                            });
                            c.Spacing(5);
                        });
                    }
                });

                page.Footer().Row(r => 
                {
                    r.RelativeItem().Text($"Generado por: {userName} - {DateTime.Now}");
                    r.RelativeItem().AlignRight().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });
            });
        }).GeneratePdf(outputPath);
    }
    public void GenerateTrainingPlan(
        StaffProfile profile, 
        List<CompetencyEvaluationDto> competencies,
        List<StaffAuthorizationDto> authorizations,
        string outputPath, 
        string userName)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                // Header
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Plan de Formación Continua").FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);
                        col.Item().Text($"Empleado: {profile.User?.FullName}").FontSize(12).Bold();
                        col.Item().Text($"Puesto: {profile.PositionTitle} | Dpto: {profile.Department}");
                    });
                    
                    row.ConstantItem(150).AlignRight().Column(col => 
                    {
                         col.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy}");
                         col.Item().Text("QMS FlowDoc ISO 15189").FontSize(8).Italic();
                    });
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Section 1: Formación Reciente
                    col.Item().Background(Colors.Grey.Lighten4).Padding(5).Text("1. Historial de Formación y Educación").Bold();
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => 
                        {
                            c.RelativeColumn(3); // Title
                            c.RelativeColumn(2); // Provider
                            c.RelativeColumn(1); // Date
                            c.RelativeColumn(0.5f); // Hours
                            c.RelativeColumn(1); // Result
                        });

                        table.Header(h => 
                        {
                            h.Cell().Element(HS).Text("Actividad");
                            h.Cell().Element(HS).Text("Proveedor");
                            h.Cell().Element(HS).Text("Fecha");
                            h.Cell().Element(HS).Text("Hrs");
                            h.Cell().Element(HS).Text("Result.");
                        });

                        foreach(var t in profile.Trainings.OrderByDescending(x => x.CompletionDate))
                        {
                            table.Cell().Element(CS).Text(t.TrainingActivity?.Title ?? "-");
                            table.Cell().Element(CS).Text(t.TrainingActivity?.Provider ?? "-");
                            table.Cell().Element(CS).Text($"{t.CompletionDate:dd/MM/yyyy}");
                            table.Cell().Element(CS).Text($"{t.TrainingActivity?.Hours}");
                            table.Cell().Element(CS).Text(t.Result ?? "-");
                        }
                    });

                    // Section 2: Competencias
                    col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(5).Text("2. Evaluación de Competencias").Bold();
                    col.Item().PaddingTop(5).Table(table =>
                    {
                         table.ColumnsDefinition(c => 
                        {
                            c.RelativeColumn(2); // Name
                            c.RelativeColumn(1.5f); // Area
                            c.RelativeColumn(1); // Date
                            c.RelativeColumn(1); // Valid Until
                            c.RelativeColumn(1); // Outcome
                        });
                        
                        table.Header(h => 
                        {
                            h.Cell().Element(HS).Text("Competencia");
                            h.Cell().Element(HS).Text("Área");
                            h.Cell().Element(HS).Text("Evaluación");
                            h.Cell().Element(HS).Text("Vencimiento");
                            h.Cell().Element(HS).Text("Estado");
                        });

                        foreach(var c in competencies.OrderBy(x => x.CompetencyName))
                        {
                            table.Cell().Element(CS).Text(c.CompetencyName ?? "-");
                            table.Cell().Element(CS).Text(c.Area ?? "-");
                            table.Cell().Element(CS).Text($"{c.EvaluationDate:dd/MM/yyyy}");
                            table.Cell().Element(CS).Text($"{c.ValidUntil:dd/MM/yyyy}");
                            
                            var statusColor = c.Outcome == "COMPETENTE" ? Colors.Green.Darken1 : Colors.Red.Darken1;
                            table.Cell().Element(CS).Text(c.Outcome ?? "-").Bold().FontColor(statusColor);
                        }
                    });

                    // Section 3: Autorizaciones
                    col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(5).Text("3. Autorizaciones Vigentes").Bold();
                    col.Item().PaddingTop(5).Table(table =>
                    {
                         table.ColumnsDefinition(c => 
                        {
                            c.RelativeColumn(2); // Task
                            c.RelativeColumn(2); // Description
                            c.RelativeColumn(1); // From
                            c.RelativeColumn(1); // Until
                            c.RelativeColumn(1.5f); // Granted By
                        });
                        
                        table.Header(h => 
                        {
                            h.Cell().Element(HS).Text("Tarea");
                            h.Cell().Element(HS).Text("Descripción");
                            h.Cell().Element(HS).Text("Desde");
                            h.Cell().Element(HS).Text("Hasta");
                            h.Cell().Element(HS).Text("Autorizado Por");
                        });

                        foreach(var a in authorizations)
                        {
                            table.Cell().Element(CS).Text(a.AuthorizationName ?? "-");
                            table.Cell().Element(CS).Text(a.Description ?? "-");
                            table.Cell().Element(CS).Text($"{a.ValidFrom:dd/MM/yyyy}");
                            table.Cell().Element(CS).Text($"{a.ValidUntil:dd/MM/yyyy}");
                            table.Cell().Element(CS).Text(a.GrantedByName ?? "-");
                        }
                    });
                });

                page.Footer().Row(r => 
                {
                    r.RelativeItem().Text($"Generado por: {userName}");
                    r.RelativeItem().AlignRight().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
                });

                static IContainer HS(IContainer c) => c.BorderBottom(1).Padding(2).DefaultTextStyle(x => x.SemiBold().FontSize(9));
                static IContainer CS(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(2).DefaultTextStyle(x => x.FontSize(9));
            });
        }).GeneratePdf(outputPath);
    }
}
