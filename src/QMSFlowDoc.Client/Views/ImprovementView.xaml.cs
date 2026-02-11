using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ImprovementView : Page
{
    private readonly IImprovementService _improvementService;
    
    public ObservableCollection<RiskListDto> Risks { get; } = new();
    public ObservableCollection<AuditListDto> Audits { get; } = new();
    public ObservableCollection<ManagementReviewListDto> Reviews { get; } = new();
    public ObservableCollection<ContingencyListDto> Contingencies { get; } = new();

    private List<RiskListDto> _allRisks = new();

    public ImprovementView()
    {
        this.InitializeComponent();
        _improvementService = ((App)Application.Current).ImprovementService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAllData();
    }

    private async Task LoadAllData()
    {
        try
        {
            var risks = await _improvementService.GetRisksAsync();
            _allRisks = risks.ToList();
            ApplyRiskFilter();

            var audits = await _improvementService.GetAuditsAsync();
            Audits.Clear();
            foreach (var a in audits) Audits.Add(a);

            var reviews = await _improvementService.GetReviewsAsync();
            Reviews.Clear();
            foreach (var r in reviews) Reviews.Add(r);

            await LoadContingencies();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading improvement data: {ex.Message}");
        }
    }

    private void ApplyRiskFilter()
    {
        Risks.Clear();
        foreach (var r in _allRisks)
        {
            if (ActiveRisksFilter.IsOn && r.Status != RiskStatus.ACTIVE) continue;
            Risks.Add(r);
        }
        BuildRiskMatrix();
    }

    private void BuildRiskMatrix()
    {
        // Guard: don't run during InitializeComponent (ToggleSwitch.IsOn triggers Toggled event)
        if (RiskMatrixGrid == null) return;

        // Remove only the programmatically added cell Borders (keep the TextBlock labels)
        var toRemove = RiskMatrixGrid.Children
            .OfType<Border>()
            .Where(b => b.Tag is string s && s == "matrixcell")
            .ToList();
        foreach (var b in toRemove) RiskMatrixGrid.Children.Remove(b);

        // Count risks per (likelihood, impact) for the currently visible set
        var counts = new int[6, 6]; // 1-indexed
        foreach (var r in Risks)
        {
            int p = (int)r.Likelihood; // 1-5
            int i = (int)r.Impact;     // 1-5
            if (p >= 1 && p <= 5 && i >= 1 && i <= 5)
                counts[p, i]++;
        }

        for (int likelihood = 5; likelihood >= 1; likelihood--)
        {
            int gridRow = 6 - likelihood; // likelihood 5 → row 1, likelihood 1 → row 5
            for (int impact = 1; impact <= 5; impact++)
            {
                int gridCol = impact; // impact 1 → col 1 ... impact 5 → col 5
                int score = likelihood * impact;
                int count = counts[likelihood, impact];

                var cellBorder = new Border
                {
                    Tag = "matrixcell",
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(GetMatrixCellColor(score)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                var cellText = new TextBlock
                {
                    Text = count > 0 ? count.ToString() : "",
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Show tooltip with score info
                ToolTipService.SetToolTip(cellBorder, $"P({likelihood}) × I({impact}) = {score}" + (count > 0 ? $" — {count} riesgo(s)" : ""));

                cellBorder.Child = cellText;
                Grid.SetRow(cellBorder, gridRow);
                Grid.SetColumn(cellBorder, gridCol);
                RiskMatrixGrid.Children.Add(cellBorder);
            }
        }
    }

    private static Windows.UI.Color GetMatrixCellColor(int score)
    {
        return score switch
        {
            <= 3  => Windows.UI.Color.FromArgb(200, 76, 175, 80),    // Green
            <= 6  => Windows.UI.Color.FromArgb(200, 255, 193, 7),    // Amber
            <= 12 => Windows.UI.Color.FromArgb(200, 255, 152, 0),    // Orange
            <= 16 => Windows.UI.Color.FromArgb(200, 244, 67, 54),    // Red
            _     => Windows.UI.Color.FromArgb(200, 183, 28, 28),    // Dark Red
        };
    }

    private void Filter_Toggled(object sender, RoutedEventArgs e)
    {
        ApplyRiskFilter();
    }

    private void AddRisk_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(RiskEditorView));
    }

    // Changed from ItemClick to DoubleTapped for editing
    private void RisksList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // Get the datum from the source if possible, or use SelectedItem
        if (RisksList.SelectedItem is RiskListDto risk)
        {
             Frame.Navigate(typeof(RiskEditorView), risk.Id);
        }
    }

    // Unused ItemClick (optional: keep for selection feedback only)
    private void RisksList_ItemClick(object sender, ItemClickEventArgs e) { }

    private async void ExportMatrix_Click(object sender, RoutedEventArgs e)
    {
        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("CSV", new List<string>() { ".csv" });
        savePicker.SuggestedFileName = "MatrizRiesgos";
        
        // Initialize the picker with the window handle (WinUI 3 requirement)
        var window = (Application.Current as App)?.MainWindow;
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            var lines = new List<string> { "Titulo,Categoria,Probabilidad,Impacto,Score,Estado" };
            foreach (var r in _allRisks)
            {
                lines.Add($"\"{r.Title}\",\"{r.Category}\",{r.Likelihood},{r.Impact},{r.Score},{r.Status}");
            }
            await Windows.Storage.FileIO.WriteLinesAsync(file, lines);
        }
    }

    private void AuditsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (AuditsList.SelectedItem is AuditListDto audit)
        {
            Frame.Navigate(typeof(AuditEditorView), audit.Id);
        }
    }

    private void AuditsList_ItemClick(object sender, ItemClickEventArgs e) { }

    private async void AddAudit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Frame.Navigate(typeof(AuditEditorView));
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error de Navegación",
                Content = $"No se pudo abrir el editor de auditoría: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void AddReview_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ReviewEditorView));
    }

    private void ReviewsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (ReviewsList.SelectedItem is ManagementReviewListDto review)
        {
            Frame.Navigate(typeof(ReviewEditorView), review.Id);
        }
    }

    private async void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        // Get the document ID from the button's Tag
        Guid? docId = null;
        
        if (sender is Button btn)
        {
            // Handle nullable Guid binding
            if (btn.Tag is Guid g && g != Guid.Empty)
            {
                docId = g;
            }
            else if (btn.Tag == null)
            {
                await ShowMessage("Aviso", "Este registro no tiene un documento adjunto todavía. Por favor, suba un documento primero desde el editor.");
                return;
            }
        }

        if (!docId.HasValue || docId.Value == Guid.Empty)
        {
            await ShowMessage("Documento no disponible", "El ID del documento es inválido o no existe. Por favor, suba un documento primero.");
            return;
        }

        // Navigate to Document Manager with the document ID to open
        // The DocumentsView will receive this ID and navigate to the document details
        Frame.Navigate(typeof(DocumentDetailsView), docId.Value);
    }

    private async void DeleteAudit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid auditId)
        {
             var confirm = await new ContentDialog
            {
                Title = "Confirmar Eliminación",
                Content = "¿Está seguro de que desea eliminar esta auditoría? Esta acción no se puede deshacer.",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            if (confirm == ContentDialogResult.Primary)
            {
                try
                {
                    var service = ((App)Application.Current).ImprovementService;
                    var success = await service.DeleteAuditAsync(auditId);
                    if (success)
                    {
                        var audit = Audits.FirstOrDefault(a => a.Id == auditId);
                        if (audit != null) Audits.Remove(audit);
                    }
                    else
                    {
                        await ShowMessage("Error", "No se pudo eliminar la auditoría.");
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessage("Error", ex.Message);
                }
            }
        }
    }

    private async void DeleteReview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid reviewId)
        {
             var confirm = await new ContentDialog
            {
                Title = "Confirmar Eliminación",
                Content = "¿Está seguro de que desea eliminar esta revisión? Esta acción no se puede deshacer.",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            if (confirm == ContentDialogResult.Primary)
            {
                try
                {
                    var service = ((App)Application.Current).ImprovementService;
                    var success = await service.DeleteReviewAsync(reviewId);
                    if (success)
                    {
                        var review = Reviews.FirstOrDefault(r => r.Id == reviewId);
                        if (review != null) Reviews.Remove(review);
                    }
                    else
                    {
                        await ShowMessage("Error", "No se pudo eliminar la revisión.");
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessage("Error", ex.Message);
                }
            }
        }
    }

    private async Task ShowMessage(string title, string content)
    {
        await new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        }.ShowAsync();
    }

    private async Task LoadContingencies()
    {
        try
        {
            var store = ((App)Application.Current).LocalStore;
            var plans = await store.GetContingencyPlansAsync();
            Contingencies.Clear();
            foreach (var p in plans) Contingencies.Add(p);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading contingencies: {ex.Message}");
        }
    }

    private async void AddContingency_Click(object sender, RoutedEventArgs e)
    {
        var stack = new StackPanel { Spacing = 10 };
        var titleBox = new TextBox { Header = "Título del Plan", PlaceholderText = "Ej: Fallo del LIS" };
        var triggerBox = new TextBox { Header = "Evento Disparador", PlaceholderText = "Ej: Caída del servidor durante > 1 hora" };
        var stepsBox = new TextBox 
        { 
            Header = "Procedimiento / Pasos", 
            PlaceholderText = "1. Notificar a soporte...\n2. Pasar a registro manual...", 
            AcceptsReturn = true, 
            TextWrapping = TextWrapping.Wrap, 
            MinHeight = 100 
        };
        var respBox = new TextBox { Header = "Responsable", PlaceholderText = "Ej: Jefe de Laboratorio" };

        stack.Children.Add(titleBox);
        stack.Children.Add(triggerBox);
        stack.Children.Add(stepsBox);
        stack.Children.Add(respBox);

        var dialog = new ContentDialog
        {
            Title = "Nuevo Plan de Contingencia",
            Content = new ScrollViewer { Content = stack, MaxHeight = 500 },
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(titleBox.Text) || string.IsNullOrWhiteSpace(triggerBox.Text))
            {
                await ShowMessage("Error", "Título y Evento Disparador son obligatorios.");
                return;
            }

            var store = ((App)Application.Current).LocalStore;
            var req = new CreateContingencyPlanRequest(
                titleBox.Text.Trim(),
                triggerBox.Text.Trim(),
                stepsBox.Text.Trim(),
                string.IsNullOrWhiteSpace(respBox.Text) ? null : respBox.Text
            );

            await store.CreateContingencyPlanAsync(req);
            await LoadContingencies();
        }
    }

    private async void ContingencyList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ContingencyListDto plan)
        {
            var statusBox = new ComboBox
            {
                Header = "Estado",
                Items = 
                { 
                    new ComboBoxItem { Content = "Borrador (Draft)", Tag = ContingencyStatus.DRAFT },
                    new ComboBoxItem { Content = "Activo", Tag = ContingencyStatus.ACTIVE },
                    new ComboBoxItem { Content = "Obsoleto", Tag = ContingencyStatus.OBSOLETE }
                },
                SelectedIndex = (int)plan.Status,
                MinWidth = 200
            };

            var dialog = new ContentDialog
            {
                Title = $"Plan: {plan.Title}",
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children = 
                    {
                        new TextBlock { Text = $"Disparador: {plan.TriggerEvent}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        statusBox,
                        new TextBlock { Text = "Cambiar a 'Activo' actualiza la fecha de revisión.", FontSize = 12, Foreground = new SolidColorBrush(Colors.White) }
                    }
                },
                PrimaryButtonText = "Actualizar",
                CloseButtonText = "Cerrar",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var newStatus = (ContingencyStatus?)((statusBox.SelectedItem as ComboBoxItem)?.Tag) ?? plan.Status;
                if (newStatus != plan.Status)
                {
                    var store = ((App)Application.Current).LocalStore;
                    await store.UpdateContingencyStatusAsync(plan.Id, newStatus);
                    await LoadContingencies();
                }
            }
        }
    }
}
