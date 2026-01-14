using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class DocumentDetailsView : Page
{
    private readonly IDocumentService _documentService;
    public Document? Document { get; private set; }

    public DocumentDetailsView()
    {
        this.InitializeComponent();
        _documentService = ((App)Application.Current).DocumentService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is Guid id)
        {
            await LoadDocument(id);
        }
    }

    private async Task LoadDocument(Guid id)
    {
        Document = await _documentService.GetDocumentByIdAsync(id);
        if (Document != null)
        {
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (Document == null) return;

        CodeText.Text = Document.DocCode;
        TitleText.Text = Document.Title;
        AreaText.Text = Document.Area ?? "N/A";
        ProcessText.Text = Document.Process ?? "N/A";
        StatusText.Text = Document.Status.ToString();

        // Update Status Badge Color
        StatusBadge.Background = Document.Status switch
        {
            DocumentStatus.DRAFT => new SolidColorBrush(Colors.Gray),
            DocumentStatus.REVIEW => new SolidColorBrush(Colors.Orange),
            DocumentStatus.APPROVED => new SolidColorBrush(Colors.Green),
            DocumentStatus.OBSOLETE => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Black)
        };

        // Update Button Visibility based on Current Status
        ApproveButton.Visibility = Document.Status == DocumentStatus.REVIEW ? Visibility.Visible : Visibility.Collapsed;
        SendReviewButton.Visibility = Document.Status == DocumentStatus.DRAFT ? Visibility.Visible : Visibility.Collapsed;
        ObsoleteButton.Visibility = Document.Status == DocumentStatus.APPROVED ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Approve_Click(object sender, RoutedEventArgs e)
    {
        await ChangeStatus(DocumentStatus.APPROVED, "Aprobado por el usuario.");
    }

    private async void SendReview_Click(object sender, RoutedEventArgs e)
    {
        await ChangeStatus(DocumentStatus.REVIEW, "Enviado a revisión.");
    }

    private async void Obsolete_Click(object sender, RoutedEventArgs e)
    {
        await ChangeStatus(DocumentStatus.OBSOLETE, "No vigente.");
    }

    // ===== ISO 15189 Document Management Actions =====

    private async void OpenWithWatermark_Click(object sender, RoutedEventArgs e)
    {
        if (Document == null) return;

        try
        {
            var app = (App)Application.Current;
            var localPath = GetLocalFilePath(Document);

            if (!File.Exists(localPath))
            {
                await ShowErrorDialog("Archivo no encontrado", 
                    "El documento no está disponible localmente. Por favor, sincroniza primero.");
                return;
            }

            await app.DocumentRenderer.OpenWithWatermarkAsync(Document, localPath);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Error al abrir documento", ex.Message);
        }
    }

    private async void PrintControlledCopy_Click(object sender, RoutedEventArgs e)
    {
        if (Document == null) return;

        // Confirm print action
        var dialog = new ContentDialog
        {
            Title = "Imprimir Copia Controlada",
            Content = $"Se generará un Copy ID único para este documento.\n\nDocumento: {Document.DocCode}\nEstado: {Document.Status}\n\n¿Deseas continuar?",
            PrimaryButtonText = "Imprimir",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        try
        {
            var app = (App)Application.Current;
            var localPath = GetLocalFilePath(Document);

            if (!File.Exists(localPath))
            {
                await ShowErrorDialog("Archivo no encontrado",
                    "El documento no está disponible localmente. Por favor, sincroniza primero.");
                return;
            }

            var printResult = await app.PrintControlService.PrintWithCopyIdAsync(Document, localPath);

            if (printResult.Success)
            {
                var successDialog = new ContentDialog
                {
                    Title = "Impresión Registrada",
                    Content = $"Copy ID generado:\n{printResult.CopyId}\n\nTimestamp: {printResult.PrintedAt:yyyy-MM-dd HH:mm:ss}\n\nSe abrirá el PDF para imprimir...",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            else
            {
                await ShowErrorDialog("Error en impresión", printResult.ErrorMessage ?? "Error desconocido");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Error al imprimir", ex.Message);
        }
    }

    private async void ExportMarkedPdf_Click(object sender, RoutedEventArgs e)
    {
        if (Document == null) return;

        try
        {
            var app = (App)Application.Current;
            var localPath = GetLocalFilePath(Document);

            if (!File.Exists(localPath))
            {
                await ShowErrorDialog("Archivo no encontrado",
                    "El documento no está disponible localmente. Por favor, sincroniza primero.");
                return;
            }

            // File picker for export
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(app.Window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("PDF Marcado", new System.Collections.Generic.List<string>() { ".pdf" });
            
            var version = Document.Versions?.OrderByDescending(v => v.VersionMajor).ThenByDescending(v => v.VersionMinor).FirstOrDefault();
            var versionLabel = version != null ? $"v{version.VersionMajor}.{version.VersionMinor}" : "v1.0";
            savePicker.SuggestedFileName = $"{Document.DocCode}_{versionLabel}_CONTROLADO";

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                var watermarkedBytes = await app.DocumentRenderer.ExportWithWatermarkAsync(Document, localPath);
                await File.WriteAllBytesAsync(file.Path, watermarkedBytes);

                var successDialog = new ContentDialog
                {
                    Title = "Exportación Exitosa",
                    Content = $"PDF marcado exportado a:\n{file.Path}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Error al exportar", ex.Message);
        }
    }

    // ===== Helper Methods =====

    private async Task ChangeStatus(DocumentStatus newStatus, string comments)
    {
        if (Document == null) return;

        var success = await _documentService.UpdateStatusAsync(Document.Id, newStatus, comments);
        if (success)
        {
            await LoadDocument(Document.Id);
        }
    }

    private string GetLocalFilePath(Document document)
    {
        // Get latest version's local file path
        var latestVersion = document.Versions?
            .OrderByDescending(v => v.VersionMajor)
            .ThenByDescending(v => v.VersionMinor)
            .FirstOrDefault();

        if (latestVersion != null && !string.IsNullOrWhiteSpace(latestVersion.LocalFilePath))
        {
            return latestVersion.LocalFilePath;
        }

        // Fallback: construct path from LocalApplicationData
        var localDocsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QMSFlowDoc", "Files", $"{document.DocCode}.pdf");

        return localDocsPath;
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
