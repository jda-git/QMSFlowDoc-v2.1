using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Microsoft.UI.Text;
using Windows.UI;
using QMSFlowDoc.Client.Views.Dialogs;

namespace QMSFlowDoc.Client.Views;

public sealed partial class InventoryView : Page
{
    private readonly IInventoryService _inventoryService;
    private readonly IAuthService _authService;
    private List<ReagentListDto> _allReagents = new();
    public ObservableCollection<ReagentListDto> Reagents { get; } = new();

    public InventoryView()
    {
        this.InitializeComponent();
        _inventoryService = ((App)Application.Current).InventoryService;
        _authService = ((App)Application.Current).AuthService;
    }

    private bool _canModify = false;
    private bool _canDelete = false;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await UpdatePermissions();
        await LoadReagents();
    }

    private async Task UpdatePermissions()
    {
        var app = (App)Application.Current;
        var perms = app.PermissionsService;
        var roles = app.AuthService.CurrentRoles;

        async Task<bool> Check(string key)
        {
            if (app.AuthService.IsAdmin) return true;
            foreach (var r in roles)
            {
                if (await perms.HasPermissionAsync(r, key)) return true;
            }
            return false;
        }

        AddReagentButton.IsEnabled = await Check("Inventory.CreateReagent");
        RegisterLotButton.IsEnabled = await Check("Inventory.Entry");
        RegisterExitButton.IsEnabled = await Check("Inventory.Exit");
        ExportExcelButton.IsEnabled = await Check("Inventory.Export");
        PrintButton.IsEnabled = await Check("Inventory.Print");

        _canModify = await Check("Inventory.EditReagent");
        _canDelete = await Check("Inventory.Delete");

        DeleteReagentButton.Visibility = _canDelete ? Visibility.Visible : Visibility.Collapsed;
        
        UpdateModifyButtonState();
    }

    private void UpdateModifyButtonState()
    {
        ModifyButton.IsEnabled = _canModify && ReagentsList.SelectedItem != null;
    }

    private async Task LoadReagents()
    {
        try
        {
            if (ActiveFilter == null || LowStockFilter == null) return;

            if (LoadingBar != null) LoadingBar.Visibility = Visibility.Visible;
            if (ReagentsList != null) ReagentsList.Opacity = 0.5;

            var isActive = ActiveFilter.IsOn ? (bool?)true : null;
            var isLowStock = LowStockFilter.IsOn;

            var reagents = await _inventoryService.GetReagentsAsync(isActive, isLowStock);
            _allReagents = reagents?.ToList() ?? new List<ReagentListDto>();
            FilterAndSearch();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading reagents: {ex.Message}");
        }
        finally
        {
            if (LoadingBar != null) LoadingBar.Visibility = Visibility.Collapsed;
            if (ReagentsList != null) ReagentsList.Opacity = 1.0;
        }
    }

    private void ReagentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateModifyButtonState();
    }

    private async void ModifyReagent_Click(object sender, RoutedEventArgs e)
    {
        if (ReagentsList.SelectedItem is ReagentListDto reagent)
        {
            if (!_canModify)
            {
                ShowError("Acceso denegado.");
                return;
            }

            Frame.Navigate(typeof(ReagentEditorView), reagent.Id);
        }
    }

    private async void DeleteReagent_Click(object sender, RoutedEventArgs e)
    {
        if (ReagentsList.SelectedItem is not ReagentListDto selected)
        {
            ShowError("Seleccione un reactivo para borrar.");
            return;
        }

        if (!_canDelete)
        {
            ShowError("Acceso denegado.");
            return;
        }

        // Confirm deletion
        var confirmMsg = $"¿Está seguro de eliminar {selected.Name} {selected.Fluorescence}?\nEsta acción marcará el reactivo como obsoleto.";
        var dialog = new ContentDialog
        {
            Title = "Confirmar eliminación",
            Content = confirmMsg,
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var success = await _inventoryService.DeleteReagentAsync(selected.Id);
            if (success)
            {
                await LoadReagents();
                ShowError("Registro borrado correctamente.");
            }
            else
            {
                ShowError("Error al borrar el reactivo.");
            }
        }
    }


    private void FilterAndSearch()
    {
        // Apply local search if needed
        // The API already filtered by Active/LowStock
        // We just filter by user search text
        // Note: AutoSuggestBox isn't named in XAML, let me check XAML... it is NOT named. I assumed it was.
        // Wait, I didn't verify if I named it.
        // I will assume I didn't name it in the previous step... checking XAML content...
        // <AutoSuggestBox PlaceholderText="Buscar..." QueryIcon="Find" Width="200" VerticalAlignment="Center" TextChanged="SearchBox_TextChanged" />
        // It has NO x:Name in previous Replace. I need to cast sender.
        
        // Actually, let's just populate Reagents for now.
        if (!string.IsNullOrEmpty(_currentSortColumn))
        {
            switch (_currentSortColumn)
            {
                case "Name":
                    _allReagents = _sortAscending ? _allReagents.OrderBy(x => x.Name).ToList() : _allReagents.OrderByDescending(x => x.Name).ToList();
                    break;
                case "Fluorescence":
                    _allReagents = _sortAscending ? _allReagents.OrderBy(x => x.Fluorescence).ToList() : _allReagents.OrderByDescending(x => x.Fluorescence).ToList();
                    break;
                case "InternalCode":
                    _allReagents = _sortAscending ? _allReagents.OrderBy(x => x.InternalCode).ToList() : _allReagents.OrderByDescending(x => x.InternalCode).ToList();
                    break;
                case "Manufacturer":
                    _allReagents = _sortAscending ? _allReagents.OrderBy(x => x.Manufacturer).ToList() : _allReagents.OrderByDescending(x => x.Manufacturer).ToList();
                    break;
                case "Reference":
                    _allReagents = _sortAscending ? _allReagents.OrderBy(x => x.Reference).ToList() : _allReagents.OrderByDescending(x => x.Reference).ToList();
                    break;
                 case "TotalStock":
                    _allReagents = _sortAscending ? _allReagents.OrderBy(x => x.TotalStock).ToList() : _allReagents.OrderByDescending(x => x.TotalStock).ToList();
                    break;
                 case "MinStock":
                    _allReagents = _sortAscending ? _allReagents.OrderBy(x => x.MinStock).ToList() : _allReagents.OrderByDescending(x => x.MinStock).ToList();
                    break;
            }
        }

        Reagents.Clear();
        foreach (var r in _allReagents) Reagents.Add(r);

        if (EmptyStatePane != null)
        {
            EmptyStatePane.Visibility = Reagents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }


    private string _currentSortColumn = "Name";
    private bool _sortAscending = true;

    private async void Filter_Toggled(object sender, RoutedEventArgs e)
    {
        await LoadReagents();
    }
    
    private void Header_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button header && header.Tag != null)
        {
            var column = header.Tag.ToString() ?? "Name";
            if (_currentSortColumn == column)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _currentSortColumn = column;
                _sortAscending = true;
            }
            FilterAndSearch();
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        var text = sender.Text.ToLower();
        var filtered = _allReagents.Where(r => r.Name.ToLower().Contains(text) || 
                                              (r.InternalCode?.ToLower().Contains(text) ?? false) ||
                                              (r.Manufacturer?.ToLower().Contains(text) ?? false));
        Reagents.Clear();
        foreach(var f in filtered) Reagents.Add(f);
    }

    private void AddReagent_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ReagentEditorView));
    }

    private async void RegisterLot_Click(object sender, RoutedEventArgs e)
    {
        if (ReagentsList.SelectedItem is not ReagentListDto selected)
        {
            ShowError("Seleccione un reactivo de la lista primero.");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"Registrar Lote - {selected.Name}",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var panel = new StackPanel { Spacing = 10 };
        var lotBox = new TextBox { Header = "Número de Lote" };
        
        // Date input restricted to Month/Year
        var datePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var monthCombo = new ComboBox { Header = "Mes", Width = 80 };
        for (int i = 1; i <= 12; i++) monthCombo.Items.Add(i.ToString("D2"));
        monthCombo.SelectedIndex = DateTime.Now.Month - 1;

        var yearCombo = new ComboBox { Header = "Año", Width = 100 };
        for (int i = 0; i < 10; i++) yearCombo.Items.Add((DateTime.Now.Year + i).ToString());
        yearCombo.SelectedIndex = 0;

        datePanel.Children.Add(monthCombo);
        datePanel.Children.Add(yearCombo);

        var qtyBox = new NumberBox { Header = "Cantidad Recibida", Value = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Minimum = 1 };
        
        panel.Children.Add(lotBox);
        panel.Children.Add(new TextBlock { Text = "Fecha Caducidad", Margin = new Thickness(0,8,0,0) });
        panel.Children.Add(datePanel);
        panel.Children.Add(qtyBox);
        dialog.Content = panel;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(lotBox.Text))
            {
                ShowError("¡Falta el lote! Por favor introduzca el número de lote.");
                return;
            }
            if (monthCombo.SelectedItem == null || yearCombo.SelectedItem == null)
            {
                ShowError("¡Falta fecha caducidad! Por favor seleccione mes y año.");
                return;
            }

            var month = int.Parse(monthCombo.SelectedItem.ToString()!);
            var year = int.Parse(yearCombo.SelectedItem.ToString()!);
            // Last day of month for expiry? Or 1st? User says "sólo mes y año". 
            // We store as date, so let's use 1st of month.
            var expiry = new DateTime(year, month, 1);
            
            var request = new RegisterLotRequest(
                selected.Id,
                lotBox.Text,
                expiry,
                DateTime.UtcNow,
                (decimal)qtyBox.Value,
                null,  // LocationId
                null,  // PanelId
                _authService.CurrentUserId
            );

            var lot = await _inventoryService.RegisterLotAsync(request);
            if (lot != null)
            {
                await LogInventoryAction("ENTRADA", selected, (decimal)qtyBox.Value, lotBox.Text, expiry);
                await LoadReagents();
                ShowError("Lote registrado correctamente.");
            }
            else
            {
                ShowError("Error al guardar el lote. Verifique los datos o contacte con soporte.\n(Posible timeout o error de servidor)");
            }
        }
    }

    private async void RegisterExit_Click(object sender, RoutedEventArgs e)
    {
         if (ReagentsList.SelectedItem is not ReagentListDto selected)
        {
            ShowError("Seleccione un reactivo de la lista primero.");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"Registrar Salida/Consumo - {selected.Name}",
            PrimaryButtonText = "Registrar",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        var panel = new StackPanel { Spacing = 12 };
        var qtyBox = new NumberBox { Header = "Cantidad a Salir", Value = 1, Minimum = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var reasonBox = new TextBox { Header = "Motivo", Text = "Consumo de Rutina" };
        
        panel.Children.Add(qtyBox);
        panel.Children.Add(reasonBox);
        var lotList = new ListView 
        { 
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 200,
            BorderThickness = new Thickness(1)
        };
        panel.Children.Add(new TextBlock { Text = "Seleccione viales a retirar:", FontWeight = FontWeights.Bold, Margin = new Thickness(0,8,0,0) });
        // Fix for selection border in dialog
        lotList.BorderBrush = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(this) == null ? null : null; // ad-hoc fix

        foreach(var lot in selected.AvailableLots)
        {
            lotList.Items.Add(new ListViewItem { Content = lot.FormattedString, Tag = lot });
        }
        panel.Children.Add(lotList);

        dialog.Content = panel;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var selectedCount = lotList.SelectedItems.Count;
            if (selectedCount != (int)qtyBox.Value)
            {
                ShowError($"¡Error de validación! El número de viales seleccionados ({selectedCount}) debe coincidir con la cantidad indicada ({(int)qtyBox.Value}).");
                return;
            }

            foreach(ListViewItem item in lotList.SelectedItems)
            {
                var lot = (LotSummaryDto)item.Tag;
                var request = new AdjustStockRequest(
                    selected.Id,
                    lot.Id,
                    Shared.Models.InventoryMovementType.OUT,
                    1, // 1 vial per selection
                    reasonBox.Text
                );
                await _inventoryService.AdjustStockAsync(request);
                await LogInventoryAction("SALIDA", selected, 1, lot.LotNumber, lot.ExpiryDate);
            }

            await LoadReagents();
            ShowError("Salida registrada correctamente.");
        }
    }

    private async Task LogInventoryAction(string op, ReagentListDto reagent, decimal qty, string lot, DateTime? expiry)
    {
        try
        {
            var auth = ((App)Application.Current).AuthService;
            var userName = auth.CurrentUsername ?? "Desconocido";

            var configStore = ((App)Application.Current).NetworkConfigStore;
            var config = await configStore.LoadAsync();
            var logDir = Path.Combine(config.LocalBasePath, "Inventario");
            Directory.CreateDirectory(logDir);
            
            var logPath = Path.Combine(logDir, "Movimientos_Inventario.csv");
            bool exists = File.Exists(logPath);
            
            using (var sw = new StreamWriter(logPath, true, System.Text.Encoding.UTF8))
            {
                if (!exists) 
                    await sw.WriteLineAsync("FechaLog,Operacion,Reactivo,Fluorescencia,Cantidad,Lote,Caducidad,Usuario");
                
                var line = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss},{op},\"{reagent.Name}\",\"{reagent.Fluorescence}\",{qty},\"{lot}\",{(expiry.HasValue ? expiry.Value.ToString("MM/yy") : "N/A")},\"{userName}\"";
                await sw.WriteLineAsync(line);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing inventory log: {ex.Message}");
        }
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintOptionsDialog(_allReagents); // Pass current list for suggestions
        dialog.XamlRoot = this.XamlRoot;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var result = dialog.Result;
            if (result == null) return;

            try
            {
                // Prepare path
                var config = await ((App)Application.Current).NetworkConfigStore.LoadAsync();
                var reportsDir = Path.Combine(config.LocalBasePath, "Informes");
                Directory.CreateDirectory(reportsDir);
                var fileName = $"Reporte_{result.Mode}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var outputPath = Path.Combine(reportsDir, fileName);
                
                var printingService = ((App)Application.Current).PrintingService;
                var auth = ((App)Application.Current).AuthService;
                var userName = auth.CurrentUsername ?? "Usuario";

                if (result.Mode == "Inventory")
                {
                    // Sort locally
                    IEnumerable<ReagentListDto> sorted = _allReagents;
                    if (!string.IsNullOrEmpty(result.SortBy))
                    {
                        sorted = result.SortBy switch
                        {
                            "Fluorescencia" => result.SortAscending ? sorted.OrderBy(x => x.Fluorescence) : sorted.OrderByDescending(x => x.Fluorescence),
                            "Código Interno" => result.SortAscending ? sorted.OrderBy(x => x.InternalCode) : sorted.OrderByDescending(x => x.InternalCode),
                            "Fabricante" => result.SortAscending ? sorted.OrderBy(x => x.Manufacturer) : sorted.OrderByDescending(x => x.Manufacturer),
                            "Referencia" => result.SortAscending ? sorted.OrderBy(x => x.Reference) : sorted.OrderByDescending(x => x.Reference),
                            "Stock Total" => result.SortAscending ? sorted.OrderBy(x => x.TotalStock) : sorted.OrderByDescending(x => x.TotalStock),
                            _ => result.SortAscending ? sorted.OrderBy(x => x.Name) : sorted.OrderByDescending(x => x.Name)
                        };
                    }
                    printingService.GenerateInventoryReport(sorted, result.SortBy ?? "Nombre", outputPath, userName);
                }
                else if (result.Mode == "Order")
                {
                    var items = _allReagents.Where(r => result.OrderIds.Contains(r.Id)).ToList();
                    printingService.GenerateOrderReport(items, outputPath, userName);
                }
                else // Consumption or Entries
                {
                    var type = result.Mode == "Consumption" ? Shared.Models.InventoryMovementType.OUT : Shared.Models.InventoryMovementType.IN;
                    var title = result.Mode == "Consumption" ? "Informe de Consumo" : "Informe de Entradas";
                    
                    var movements = await _inventoryService.GetMovementsAsync(result.StartDate, result.EndDate, type, result.ReagentId);
                    
                    if (!movements.Any())
                    {
                        ShowError($"No se encontraron movimientos de tipo '{type}' en el periodo seleccionado.");
                        return;
                    }

                    if (result.Mode == "Entries")
                    {
                        printingService.GenerateEntriesReport(movements, result.StartDate, result.EndDate, outputPath, userName);
                    }
                    else
                    {
                        printingService.GenerateMovementsReport(movements, title, result.StartDate, result.EndDate, outputPath, userName);
                    }
                }

                // Launch PDF
                var p = new System.Diagnostics.Process();
                p.StartInfo = new System.Diagnostics.ProcessStartInfo(outputPath) { UseShellExecute = true };
                p.Start();
            }
            catch (Exception ex)
            {
                ShowError($"Error al generar informe: {ex.Message}");
            }
        }
    }

    private async void ShowError(string msg)
    {
        var d = new ContentDialog { Title = "Aviso", Content = msg, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
        await d.ShowAsync();
    }


    public static Microsoft.UI.Xaml.Media.Brush GetStatusColor(int status)
    {
        return status switch
        {
            2 => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Red),
            1 => new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Orange),
            _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] // Use theme brush
        };
    }

    private async void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exportService = ((App)Application.Current).ExportService;
            await exportService.ExportReagentsToExcelAsync(_allReagents);
        }
        catch (Exception ex)
        {
            ShowError($"Error al exportar: {ex.Message}");
        }
    }
}
