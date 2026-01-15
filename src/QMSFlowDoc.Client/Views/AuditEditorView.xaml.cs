using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using Windows.Storage.Pickers;
using System.Linq;
using System.IO;

namespace QMSFlowDoc.Client.Views;

public sealed partial class AuditEditorView : Page
{
    public AuditEditorView()
    {
        this.InitializeComponent();
        FindingsList.ItemsSource = Findings;
    }

    private Guid? _auditId;
    private Guid? _reportDocumentId;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            _auditId = id;
            await LoadAudit(id);
        }
    }

    private System.Collections.ObjectModel.ObservableCollection<AuditFinding> Findings { get; set; } = new();

    private async System.Threading.Tasks.Task LoadAudit(Guid id)
    {
        try
        {
            var service = ((App)Application.Current).ImprovementService;
            var audit = await service.GetAuditByIdAsync(id);
            if (audit != null)
            {
                TitleBox.Text = audit.Title;
                ScheduledDatePicker.Date = audit.ScheduledDate;
                ScopeBox.Text = audit.Scope;
                AuditorBox.Text = audit.LeadAuditor;
                _reportDocumentId = audit.ReportDocumentId;

                // Load Findings
                Findings.Clear();
                if (audit.Findings != null)
                {
                    foreach (var f in audit.Findings) Findings.Add(f);
                }
                FindingsList.ItemsSource = Findings;

                if (audit.ReportDocument != null)
                {
                    FileNameText.Text = audit.ReportDocument.Title; // Or OriginalFileName if available
                    // Show download/view button if possible
                    ViewReportButton.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
             ErrorText.Text = $"Error loading audit: {ex.Message}";
             ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void AddFinding_Click(object sender, RoutedEventArgs e)
    {
        if (!_auditId.HasValue)
        {
             await new ContentDialog 
            { 
                Title = "Guardar Primero", 
                Content = "Por favor, guarde el plan de auditoría antes de añadir hallazgos.", 
                CloseButtonText = "OK", 
                XamlRoot = this.XamlRoot 
            }.ShowAsync();
            return;
        }

        var dialog = new Dialogs.AddFindingDialog();
        dialog.XamlRoot = this.XamlRoot;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var req = new RegisterFindingRequest(
                    _auditId.Value,
                    dialog.Description,
                    dialog.IsoRequirement,
                    dialog.FindingType,
                    null // RelatedNCId can be implemented later if we link directly to NC creation
                );
                
                var service = ((App)Application.Current).ImprovementService;
                var finding = await service.RegisterFindingAsync(req);
                
                if (finding != null)
                {
                    Findings.Add(finding);
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Error creating finding: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            ErrorText.Text = "El título es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var request = new CreateAuditRequest(
                TitleBox.Text,
                ScheduledDatePicker.Date.UtcDateTime,
                ScopeBox.Text,
                AuditorBox.Text,
                _reportDocumentId
            );

            var service = ((App)Application.Current).ImprovementService;

            if (_auditId.HasValue)
            {
                var success = await service.UpdateAuditAsync(_auditId.Value, request);
                if (success) Frame.GoBack();
                else { ErrorText.Text = "Error al actualizar."; ErrorText.Visibility = Visibility.Visible; }
            }
            else
            {
                var result = await service.CreateAuditAsync(request);
                if (result != null)
                {
                    Frame.GoBack();
                }
                else
                {
                    ErrorText.Text = "Error al guardar.";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void ViewReport_Click(object sender, RoutedEventArgs e)
    {
        // Implementation preserved as placeholder for now
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void UploadReport_Click(object sender, RoutedEventArgs e)
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
                
                // Refresh connection status to ensure we use API if available
                await docService.InitializeAsync();

                if (docService.IsLocalMode)
                {
                    FileNameText.Text = "Error: Modo Offline no permite vincular documentos a Auditoría Online.";
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

                // 2. Create Document Metadata
                var createReq = new CreateDocumentRequest(
                   DocCode: "AUD-" + DateTime.Now.Ticks.ToString().Substring(10),
                   Title: $"Reporte Auditoría {TitleBox.Text}",
                   DocumentTypeId: reportType.Id,
                   FolderId: folderId, // Use AUDITORIA folder or null
                   Area: "Improvement",
                   Process: "Audit",
                   Status: DocumentStatus.APPROVED, // Auto-approved for records
                   ReviewIntervalMonths: 12, 
                   VersionLabel: "v1.0"
                );
                
                var doc = await docService.CreateDocumentAsync(createReq);
                if (doc != null)
                {
                     // 2. Upload Content
                    using var stream = await file.OpenStreamForReadAsync();
                    var bytes = new byte[stream.Length];
                    await stream.ReadAsync(bytes, 0, bytes.Length);
                    
                    var success = await docService.UploadFileAsync(doc.Id, bytes, file.Name, file.ContentType);
                    if (success) 
                    {
                        _reportDocumentId = doc.Id;
                        FileNameText.Text = file.Name;
                        ViewReportButton.Visibility = Visibility.Visible;
                        
                        // Auto-save the audit link immediately to prevent sync issues if user forgets to click Save
                        if (_auditId.HasValue)
                        {
                            // Trigger implicit save of the link
                            // Actually, better wait for explicit save, but show success
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

}
