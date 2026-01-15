using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Linq;
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

                if (docService.IsLocalMode)
                {
                    FileNameText.Text = "Error: Modo Offline no permite vincular documentos a Revisión Online.";
                    return;
                }

                // 1. Fetch Document Types
                var typeList = await docService.GetDocumentTypesAsync();
                var reportType = typeList.FirstOrDefault(t => t.Name == "Reporte") ?? typeList.FirstOrDefault();
                
                if (reportType == null) throw new Exception("No se encontraron tipos de documentos configurados.");

                // Lookup Folder
                var folderId = await docService.GetOrCreateFolderIdAsync("AUDITORIA"); 
                
                if (folderId == null)
                {
                    FileNameText.Text = "Error: No se pudo crear/encontrar la carpeta 'AUDITORIA'.";
                    return;
                }

                var createReq = new CreateDocumentRequest(
                   DocCode: "REV-" + DateTime.Now.Ticks.ToString().Substring(10),
                   Title: $"Acta Revisión {ReviewDatePicker.Date.Year}",
                   DocumentTypeId: reportType.Id,
                   FolderId: folderId, 
                   Area: "Management",
                   Process: "Review",
                   Status: DocumentStatus.APPROVED,
                   ReviewIntervalMonths: 12,
                   VersionLabel: "v1.0"
                );
                
                var doc = await docService.CreateDocumentAsync(createReq);
                if (doc != null)
                {
                    using var stream = await file.OpenStreamForReadAsync();
                    var bytes = new byte[stream.Length];
                    await stream.ReadAsync(bytes, 0, bytes.Length);
                    
                    var success = await docService.UploadFileAsync(doc.Id, bytes, file.Name, file.ContentType);
                    if (success) 
                    {
                        _minutesDocumentId = doc.Id;
                        FileNameText.Text = file.Name;
                        
                        // Auto-save hint
                        if (_reviewId.HasValue)
                        {
                             FileNameText.Text = $"{file.Name} (Vinculado - Recuerde Guardar)";
                        }
                    }
                    else
                    {
                        FileNameText.Text = "Error al subir contenido al servidor.";
                    }
                }
                else
                {
                    FileNameText.Text = "Error: El servidor rechazó la creación del documento.";
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
        if (e.Parameter is Guid id)
        {
            _reviewId = id;
            await LoadReview(id);
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
