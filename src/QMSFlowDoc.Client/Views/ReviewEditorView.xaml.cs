using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Client.Services;
using System;
using System.Linq;
using System.Collections.Generic;
using Windows.Storage.Pickers;
using System.IO;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ReviewEditorView : Page
{
    private Guid? _minutesDocumentId;

    public ReviewEditorView()
    {
        this.InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void UploadMinutes_Click(object sender, RoutedEventArgs e)
    {
        var openPicker = new FileOpenPicker();
        var window = (Application.Current as App)?.MainWindow;
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        openPicker.FileTypeFilter.Add(".pdf");
        openPicker.FileTypeFilter.Add(".docx");
        openPicker.FileTypeFilter.Add(".doc");

        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            FileNameText.Text = "Subiendo...";
            try
            {
                var docService = ((App)Application.Current).DocumentService;
                
                // Refresh connection status
                await docService.InitializeAsync();

                // Removed explicit LocalMode check


                // 1. Fetch Document Types
                // 1. Fetch Document Types
                var typeList = await docService.GetDocumentTypesAsync();
                var reportType = typeList.FirstOrDefault(t => t.Name == "Reporte") ?? typeList.FirstOrDefault();

                // 2. Prepare Folder Name (flat REVISION DIRECCION folder)
                var folderName = "REVISION DIRECCION";
                
                // 3. Prepare filename as MM-YYYY-Acta_Revision.pdf
                var date = ReviewDatePicker.Date;
                var datePrefix = $"{date.Month:D2}-{date.Year}";
                var safeName = $"{datePrefix}-Acta_Revision_Direccion.pdf";
                
                // 4. Read File
                using var stream = await file.OpenStreamForReadAsync();
                var bytes = new byte[stream.Length];
                await stream.ReadAsync(bytes, 0, bytes.Length);

                // 5. Create directly
                var doc = await docService.CreateDocumentWithFileAsync(
                   docCode: $"REV-{datePrefix}-{DateTime.Now.Ticks.ToString().Substring(12)}",
                   title: $"Acta Revisión Dirección {datePrefix}",
                   status: DocumentStatus.APPROVED,
                   documentTypeId: reportType?.Id,
                   reviewIntervalMonths: 12,
                   versionLabel: "v1.0",
                   area: "Management",
                   process: "Review",
                   fileBytes: bytes,
                   fileName: safeName,
                   subFolderName: folderName
                );
                
                if (doc != null)
                {
                    _minutesDocumentId = doc.Id;
                    FileNameText.Text = file.Name;
                    
                    // Auto-save the link
                    if (_reviewId.HasValue)
                    {
                        try {
                            var updateReq = new CreateManagementReviewRequest(
                                ReviewDatePicker.Date.UtcDateTime,
                                ParticipantsBox.Text,
                                AgendaBox.Text,
                                SummaryBox.Text,
                                ActionsBox.Text,
                                _minutesDocumentId
                            );
                            await ((App)Application.Current).ImprovementService.UpdateReviewAsync(_reviewId.Value, updateReq);
                            FileNameText.Text = $"{file.Name} (Guardado)";
                        } catch {
                            FileNameText.Text = $"{file.Name} (Vinculado - Recuerde Guardar)";
                        }
                    }
                }
                else
                {
                    FileNameText.Text = "Error: El sistema no pudo crear el documento.";
                }
            }
            catch (Exception ex)
            {
                FileNameText.Text = "Error Crítico: " + ex.Message;
            }
        }
    }

    private Guid? _reviewId;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAnnualSummaryAsync();
        if (e.Parameter is Guid id)
        {
            _reviewId = id;
            await LoadReview(id);
        }
    }

    private async System.Threading.Tasks.Task LoadAnnualSummaryAsync()
    {
        try
        {
            var dashboardService = ((App)Application.Current).DashboardService;
            var data = await dashboardService.GetDashboardDataAsync();
            if (data?.Stats != null)
            {
                var s = data.Stats;
                SummaryNC.Text = $"NC pendientes revisión: {s.PendingReviewDocs} | Aprobación: {s.PendingApprovalDocs}";
                SummaryRisks.Text = $"Riesgos altos activos: {s.OpenHighRisks}";
                SummaryEQA.Text = $"EQA activos: {s.ActiveEQAPrograms} | Pendientes: {s.PendingEQAResults}";
                SummaryCompetencies.Text = $"Competencias vencidas: {s.ExpiredCompetencies} | Formaciones pend.: {s.PendingTrainings}";
                SummaryDocs.Text = $"Documentos totales: {s.TotalDocuments}";
                SummaryEquipment.Text = $"Mtto. equipos vencido: {s.DueEquipmentMaintenance}";
            }

            // Also load audit/risk counts for the consolidated view
            var improvService = ((App)Application.Current).ImprovementService;
            var audits = await improvService.GetAuditsAsync();
            var auditList = audits.ToList();
            int completed = auditList.Count(a => a.Status == AuditStatus.COMPLETED);
            int totalFindings = auditList.Sum(a => a.FindingCount);
            SummaryAudits.Text = $"Auditorías completadas: {completed}/{auditList.Count} | Hallazgos: {totalFindings}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading summary: {ex.Message}");
            SummaryNC.Text = "No conformidades: datos no disponibles";
            SummaryRisks.Text = "Riesgos: datos no disponibles";
            SummaryAudits.Text = "Auditorías: datos no disponibles";
            SummaryEQA.Text = "EQA: datos no disponibles";
            SummaryCompetencies.Text = "Competencias: datos no disponibles";
            SummaryDocs.Text = "Documentos: datos no disponibles";
            SummaryEquipment.Text = "Equipos: datos no disponibles";
        }
    }

    private async System.Threading.Tasks.Task LoadReview(Guid id)
    {
        try
        {
            var service = ((App)Application.Current).ImprovementService;
            var review = await service.GetReviewByIdAsync(id);
            if (review != null)
            {
                ReviewDatePicker.Date = review.ReviewDate;
                ParticipantsBox.Text = review.Participants;
                AgendaBox.Text = review.Agenda;
                SummaryBox.Text = review.Summary;
                ActionsBox.Text = review.Actions;
                _minutesDocumentId = review.MinutesDocumentId;

                if (review.MinutesDocument != null)
                {
                    FileNameText.Text = review.MinutesDocument.Title;
                }
            }
        }
        catch (Exception ex)
        {
             ErrorText.Text = $"Error loading review: {ex.Message}";
             ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(SummaryBox.Text))
        {
            ErrorText.Text = "El resumen es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var request = new CreateManagementReviewRequest(
                ReviewDatePicker.Date.UtcDateTime,
                ParticipantsBox.Text,
                AgendaBox.Text,
                SummaryBox.Text,
                ActionsBox.Text,
                _minutesDocumentId
            );

            var service = ((App)Application.Current).ImprovementService;

            if (_reviewId.HasValue)
            {
                 var success = await service.UpdateReviewAsync(_reviewId.Value, request);
                 if (success) Frame.GoBack();
                 else { ErrorText.Text = "Error al actualizar."; ErrorText.Visibility = Visibility.Visible; }
            }
            else
            {
                var result = await service.CreateReviewAsync(request);
                if (result != null) Frame.GoBack();
                else { ErrorText.Text = "Error al guardar."; ErrorText.Visibility = Visibility.Visible; }
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
