using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace QMSFlowDoc.Client.Views;

public sealed partial class SuppliersView : Page
{
    public ObservableCollection<SupplierListDto> AllSuppliers { get; } = new();
    public ObservableCollection<SupplierListDto> FilteredSuppliers { get; } = new();

    public SuppliersView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadSuppliersAsync();
    }

    private async System.Threading.Tasks.Task LoadSuppliersAsync()
    {
        LoadingBar.Visibility = Visibility.Visible;
        try
        {
            var app = (App)Application.Current;
            var suppliers = await app.SupplierService.GetSuppliersAsync();
            
            AllSuppliers.Clear();
            foreach (var s in suppliers) AllSuppliers.Add(s);
            
            ApplyFilters();
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyFilters()
    {
        // Guard: Skip if controls aren't initialized yet (XAML fires SelectionChanged during init)
        if (TypeFilterCombo == null || StatusFilterCombo == null || SearchBox == null) return;
        
        FilteredSuppliers.Clear();
        var filtered = AllSuppliers.AsEnumerable();

        // Type filter
        var typeIndex = TypeFilterCombo.SelectedIndex;
        if (typeIndex > 0)
        {
            var targetType = (SupplierType)(typeIndex - 1);
            filtered = filtered.Where(s => s.Type == targetType);
        }

        // Status filter
        var statusIndex = StatusFilterCombo.SelectedIndex;
        if (statusIndex > 0)
        {
            SupplierQualityStatus targetStatus = statusIndex switch
            {
                1 => SupplierQualityStatus.APTO,
                2 => SupplierQualityStatus.NO_APTO,
                3 => SupplierQualityStatus.PENDIENTE,
                4 => SupplierQualityStatus.EVALUACION_CADUCADA,
                _ => SupplierQualityStatus.PENDIENTE
            };
            filtered = filtered.Where(s => s.QualityStatus == targetStatus);
        }

        // Search filter
        var search = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var s in filtered) FilteredSuppliers.Add(s);
        EmptyState.Visibility = FilteredSuppliers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SupplierEditorView));
    }

    private void EditSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (SuppliersList.SelectedItem is SupplierListDto supplier)
        {
            Frame.Navigate(typeof(SupplierEditorView), supplier.Id);
        }
    }

    private async void EvaluateSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (SuppliersList.SelectedItem is not SupplierListDto supplier)
        {
            await ShowMessage("Selección requerida", "Por favor, seleccione un proveedor para evaluar.");
            return;
        }

        // Show evaluation dialog
        var dialog = new SupplierEvaluationDialog(supplier.Id, supplier.Name)
        {
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await LoadSuppliersAsync();
        }
    }

    private async void DeleteSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (SuppliersList.SelectedItem is not SupplierListDto supplier)
        {
            await ShowMessage("Selección requerida", "Por favor, seleccione un proveedor para eliminar.");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Confirmar eliminación",
            Content = $"¿Está seguro de que desea eliminar el proveedor '{supplier.Name}'? Se eliminarán también todas sus evaluaciones.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var app = (App)Application.Current;
            if (await app.SupplierService.DeleteSupplierAsync(supplier.Id))
            {
                await LoadSuppliersAsync();
            }
        }
    }

    private async System.Threading.Tasks.Task ShowMessage(string title, string content)
    {
        var dlg = new ContentDialog { Title = title, Content = content, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
        await dlg.ShowAsync();
    }

    // Helper methods for XAML bindings
    public static SolidColorBrush GetStatusColor(SupplierQualityStatus status) => status switch
    {
        SupplierQualityStatus.APTO => new SolidColorBrush(Microsoft.UI.Colors.Green),
        SupplierQualityStatus.NO_APTO => new SolidColorBrush(Microsoft.UI.Colors.Red),
        SupplierQualityStatus.EN_OBSERVACION => new SolidColorBrush(Microsoft.UI.Colors.Orange),
        SupplierQualityStatus.PENDIENTE => new SolidColorBrush(Microsoft.UI.Colors.Gray),
        SupplierQualityStatus.EVALUACION_CADUCADA => new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
        _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
    };

    public static SolidColorBrush GetIncidentColor(int count) => count switch
    {
        0 => new SolidColorBrush(Microsoft.UI.Colors.Green),
        <= 2 => new SolidColorBrush(Microsoft.UI.Colors.Orange),
        _ => new SolidColorBrush(Microsoft.UI.Colors.Red)
    };
}
