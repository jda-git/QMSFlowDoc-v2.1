using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class IQCView : Page
{
    public ObservableCollection<IQCListDto> Results { get; } = new();
    private List<IQCListDto> _allResults = new();

    public IQCView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadResults();
    }

    private async Task LoadResults()
    {
        try
        {
            var store = ((App)Application.Current).LocalStore;
            var list = await store.GetIQCResultsAsync();
            _allResults = list;

            // Populate filters
            var equipments = _allResults.Select(r => r.EquipmentName).Distinct().OrderBy(x => x).ToList();
            var analytes = _allResults.Select(r => r.AnalyteName).Distinct().OrderBy(x => x).ToList();

            EquipmentFilter.Items.Clear();
            EquipmentFilter.Items.Add(new ComboBoxItem { Content = "Todos" });
            foreach (var eq in equipments) EquipmentFilter.Items.Add(new ComboBoxItem { Content = eq });

            AnalyteFilter.Items.Clear();
            AnalyteFilter.Items.Add(new ComboBoxItem { Content = "Todos" });
            foreach (var an in analytes) AnalyteFilter.Items.Add(new ComboBoxItem { Content = an });

            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading IQC: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        Results.Clear();
        var equipFilter = (EquipmentFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var analyteFilter = (AnalyteFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();

        foreach (var r in _allResults)
        {
            if (!string.IsNullOrEmpty(equipFilter) && equipFilter != "Todos" && r.EquipmentName != equipFilter) continue;
            if (!string.IsNullOrEmpty(analyteFilter) && analyteFilter != "Todos" && r.AnalyteName != analyteFilter) continue;
            Results.Add(r);
        }
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_allResults.Count > 0) ApplyFilter();
    }

    private async void AddIQC_Click(object sender, RoutedEventArgs e)
    {
        var stack = new StackPanel { Spacing = 10 };
        var equipBox = new TextBox { Header = "Equipo", PlaceholderText = "Ej: Cobas c311" };
        var analyteBox = new TextBox { Header = "Analito", PlaceholderText = "Ej: Glucosa" };
        var levelBox = new ComboBox { Header = "Nivel", Items = { "Normal", "Patológico Bajo", "Patológico Alto" }, SelectedIndex = 0 };
        var valueBox = new NumberBox { Header = "Valor Obtenido", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var meanBox = new NumberBox { Header = "Media Asignada", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var sdBox = new NumberBox { Header = "DE (Desviación Estándar)", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var commentsBox = new TextBox { Header = "Comentarios (Opcional)", PlaceholderText = "Observaciones" };

        stack.Children.Add(equipBox);
        stack.Children.Add(analyteBox);
        stack.Children.Add(levelBox);
        stack.Children.Add(valueBox);
        stack.Children.Add(meanBox);
        stack.Children.Add(sdBox);
        stack.Children.Add(commentsBox);

        var scrollViewer = new ScrollViewer { Content = stack, MaxHeight = 450 };

        var dialog = new ContentDialog
        {
            Title = "Registrar Resultado IQC (Westgard)",
            Content = scrollViewer,
            PrimaryButtonText = "Registrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(equipBox.Text) || string.IsNullOrWhiteSpace(analyteBox.Text))
            {
                var errDlg = new ContentDialog { Title = "Error", Content = "Equipo y Analito son obligatorios.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await errDlg.ShowAsync();
                return;
            }

            var store = ((App)Application.Current).LocalStore;
            var req = new CreateIQCResultRequest(
                equipBox.Text.Trim(),
                analyteBox.Text.Trim(),
                (levelBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal",
                valueBox.Value,
                meanBox.Value,
                sdBox.Value,
                DateTime.UtcNow,
                string.IsNullOrWhiteSpace(commentsBox.Text) ? null : commentsBox.Text
            );

            var result = await store.CreateIQCResultAsync(req);

            // Show Westgard result
            string statusMsg = result.Status switch
            {
                IQCStatus.REJECTED => $"⛔ RECHAZADO — Regla {result.WestgardRule} violada. Resultado fuera de ±3DE.",
                IQCStatus.WARNING => $"⚠️ ALERTA — Regla {result.WestgardRule}. Resultado entre ±2DE y ±3DE.",
                _ => "✅ OK — Resultado dentro de ±2DE."
            };

            var resultDlg = new ContentDialog
            {
                Title = "Evaluación Westgard",
                Content = statusMsg,
                CloseButtonText = "Aceptar",
                XamlRoot = this.XamlRoot
            };
            await resultDlg.ShowAsync();

            await LoadResults();
        }
    }
}
