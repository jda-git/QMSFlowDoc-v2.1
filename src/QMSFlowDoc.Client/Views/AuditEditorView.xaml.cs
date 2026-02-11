using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage.Pickers;
using System.Linq;
using System.IO;

namespace QMSFlowDoc.Client.Views;

public sealed partial class AuditEditorView : Page
{
    private List<ChecklistItem> _checklistItems = new();

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

                // Load Checklist
                if (!string.IsNullOrEmpty(audit.ChecklistJson))
                {
                    try { _checklistItems = JsonSerializer.Deserialize<List<ChecklistItem>>(audit.ChecklistJson) ?? new(); }
                    catch { _checklistItems = new(); }
                }
                RebuildChecklistUI();

                // Load Findings
                Findings.Clear();
                if (audit.Findings != null)
                {
                    foreach (var f in audit.Findings) Findings.Add(f);
                }
                FindingsList.ItemsSource = Findings;

                if (audit.ReportDocument != null)
                {
                    FileNameText.Text = audit.ReportDocument.Title;
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
            var checklistJson = _checklistItems.Count > 0 
                ? JsonSerializer.Serialize(_checklistItems) 
                : null;

            var request = new CreateAuditRequest(
                TitleBox.Text,
                ScheduledDatePicker.Date.UtcDateTime,
                ScopeBox.Text,
                AuditorBox.Text,
                _reportDocumentId,
                checklistJson
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
        if (_reportDocumentId.HasValue)
        {
            try
            {
                var docService = ((App)Application.Current).DocumentService;
                var bytes = await docService.GetFileContentAsync(_reportDocumentId.Value);

                if (bytes != null && bytes.Length > 0)
                {
                    var tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                    var file = await tempFolder.CreateFileAsync($"audit_report_{_reportDocumentId}.pdf", Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                    await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
                    
                    await Windows.System.Launcher.LaunchFileAsync(file);
                }
                else
                {
                     // Try local fallback message
                    var dialog = new ContentDialog
                    {
                        Title = "Documento no encontrado",
                        Content = "No se pudo recuperar el archivo. Verifique si se subió correctamente.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                 var dialog = new ContentDialog
                 {
                     Title = "Error al abrir",
                     Content = ex.Message,
                     CloseButtonText = "OK",
                     XamlRoot = this.XamlRoot
                 };
                 await dialog.ShowAsync();
            }
        }
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

                // Removed explicit LocalMode check to allow fallback to LocalDocumentStore


                // 1. Fetch Document Types
                var typeList = await docService.GetDocumentTypesAsync();
                var reportType = typeList.FirstOrDefault(t => t.Name == "Reporte") ?? typeList.FirstOrDefault();
                
                // 2. Prepare Folder Name (flat AUDITORIA folder)
                var folderName = "AUDITORIA";
                
                // 3. Prepare filename as MM-YYYY-Name.pdf
                var date = ScheduledDatePicker.Date;
                var datePrefix = $"{date.Month:D2}-{date.Year}";
                var safeName = $"{datePrefix}-{TitleBox.Text.Replace(" ", "_")}.pdf";

                // 4. Read File
                using var stream = await file.OpenStreamForReadAsync();
                var bytes = new byte[stream.Length];
                await stream.ReadAsync(bytes, 0, bytes.Length);

                // 5. Create Document directly
                var doc = await docService.CreateDocumentWithFileAsync(
                   docCode: $"AUD-{datePrefix}-{DateTime.Now.Ticks.ToString().Substring(12)}",
                   title: $"Reporte Auditoría {TitleBox.Text}",
                   status: DocumentStatus.APPROVED,
                   documentTypeId: reportType?.Id,
                   reviewIntervalMonths: 12, 
                   versionLabel: "v1.0",
                   area: "Improvement",
                   process: "Audit",
                   fileBytes: bytes,
                   fileName: safeName,
                   subFolderName: folderName
                );
                
                if (doc != null)
                {
                    _reportDocumentId = doc.Id;
                    FileNameText.Text = file.Name;
                    ViewReportButton.Visibility = Visibility.Visible;
                    
                    // Auto-save the audit link
                    if (_auditId.HasValue)
                    {
                        try {
                            var updateReq = new CreateAuditRequest(
                                TitleBox.Text,
                                ScheduledDatePicker.Date.UtcDateTime,
                                ScopeBox.Text,
                                AuditorBox.Text,
                                _reportDocumentId,
                                _checklistItems.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(_checklistItems) : null
                            );
                            await ((App)Application.Current).ImprovementService.UpdateAuditAsync(_auditId.Value, updateReq);
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

    private async void AddChecklistItem_Click(object sender, RoutedEventArgs e)
    {
        var questionBox = new TextBox 
        { 
            Header = "Pregunta de Verificación", 
            PlaceholderText = "Ej: ¿Se calibran los equipos según plan?",
            AcceptsReturn = false 
        };

        var dialog = new ContentDialog
        {
            Title = "Nuevo Ítem de Checklist",
            Content = questionBox,
            PrimaryButtonText = "Añadir",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(questionBox.Text))
        {
            _checklistItems.Add(new ChecklistItem { Question = questionBox.Text });
            RebuildChecklistUI();
        }
    }

    private void RebuildChecklistUI()
    {
        ChecklistListView.Items.Clear();
        for (int idx = 0; idx < _checklistItems.Count; idx++)
        {
            var item = _checklistItems[idx];
            int capturedIdx = idx;

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(100, GridUnitType.Pixel) },
                    new ColumnDefinition { Width = new GridLength(200, GridUnitType.Pixel) },
                    new ColumnDefinition { Width = new GridLength(32, GridUnitType.Pixel) }
                },
                Padding = new Thickness(8, 4, 8, 4)
            };

            var questionText = new TextBlock
            {
                Text = item.Question,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            var answerCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = item.Answer switch { "Sí" => 0, "No" => 1, "N/A" => 2, _ => -1 }
            };
            answerCombo.Items.Add("Sí");
            answerCombo.Items.Add("No");
            answerCombo.Items.Add("N/A");
            answerCombo.SelectionChanged += (s, args) =>
            {
                if (answerCombo.SelectedItem is string val)
                    _checklistItems[capturedIdx].Answer = val;
            };

            var obsBox = new TextBox
            {
                PlaceholderText = "Observaciones...",
                Text = item.Observations ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            obsBox.TextChanged += (s, args) =>
            {
                _checklistItems[capturedIdx].Observations = obsBox.Text;
            };

            var deleteBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 },
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4)
            };
            deleteBtn.Click += (s, args) =>
            {
                _checklistItems.RemoveAt(capturedIdx);
                RebuildChecklistUI();
            };

            Grid.SetColumn(questionText, 0);
            Grid.SetColumn(answerCombo, 1);
            Grid.SetColumn(obsBox, 2);
            Grid.SetColumn(deleteBtn, 3);

            row.Children.Add(questionText);
            row.Children.Add(answerCombo);
            row.Children.Add(obsBox);
            row.Children.Add(deleteBtn);

            ChecklistListView.Items.Add(row);
        }
        ChecklistCountText.Text = $"{_checklistItems.Count} ítem(s)";
    }
}

public class ChecklistItem
{
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }  // "Sí", "No", "N/A"
    public string? Observations { get; set; }
}
