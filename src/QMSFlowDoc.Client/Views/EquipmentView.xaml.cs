using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace QMSFlowDoc.Client.Views;

public sealed partial class EquipmentView : Page
{
    private readonly IEquipmentService _equipmentService;
    private readonly Services.IAuditLogger _auditLogger;
    private readonly Services.IAuthService _authService;
    public ObservableCollection<EquipmentListDto> Equipment { get; } = new();
    private Guid? _editingMaintenanceId = null;

    public EquipmentView()
    {
        this.InitializeComponent();
        _equipmentService = ((App)Application.Current).EquipmentService;
        _auditLogger = ((App)Application.Current).EquipmentAuditLogger;
        _authService = ((App)Application.Current).AuthService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadEquipment();
    }

    private async Task LoadEquipment()
    {
        try
        {
            LoadingBar.Visibility = Visibility.Visible;
            if (EquipmentList != null) EquipmentList.Opacity = 0.5;

            var list = await _equipmentService.GetEquipmentAsync();
            Equipment.Clear();
            foreach (var item in list)
            {
                Equipment.Add(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading equipment: {ex.Message}");
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            if (EquipmentList != null) EquipmentList.Opacity = 1.0;
            
            if (EmptyStatePane != null)
            {
                EmptyStatePane.Visibility = Equipment.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }



    private void AddEquipment_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(EquipmentEditorView));
    }

    private async void RegisterMaintenance_Click(object sender, RoutedEventArgs e)
    {
        // Check admin role instead of password
        if (!_authService.IsAdmin)
        {
            await ShowErrorDialog("Acceso denegado. Se requieren permisos de administrador.");
            return;
        }

        MaintEquipmentCombo.ItemsSource = Equipment;
        if (Equipment.Count > 0) MaintEquipmentCombo.SelectedIndex = 0;
        
        // Reset dialog fields (they will be populated by SelectionChanged if equipment has maintenance)
        _editingMaintenanceId = null;
        MaintNotesBox.Text = "";
        MaintDatePicker.Date = DateTimeOffset.Now;
        MaintHasIssuesCheck.IsChecked = false;
        MaintOutcomeCombo.SelectedIndex = 0; // Default to Pass

        // Populate year combo for next maintenance
        NextMaintYearCombo.Items.Clear();
        for (int i = 0; i < 5; i++)
        {
            var year = DateTime.Now.Year + i;
            var item = new ComboBoxItem { Content = year.ToString(), Tag = year };
            NextMaintYearCombo.Items.Add(item);
        }
        NextMaintYearCombo.SelectedIndex = 0;
        NextMaintMonthCombo.SelectedIndex = DateTime.Now.Month - 1;

        await MaintenanceDialog.ShowAsync();
    }

    private async void MaintEquipmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MaintEquipmentCombo.SelectedItem is EquipmentListDto selected)
        {
            // Ensure we are in "Create New" mode by default
            _editingMaintenanceId = null;
            
            // Clean fields initially
            MaintNotesBox.Text = "";
            MaintHasIssuesCheck.IsChecked = false;

            try 
            {
               // Feature Request: Pre-fill notes from previous maintenance
               var lastMaint = await _equipmentService.GetLastMaintenanceAsync(selected.Id);
               if (lastMaint != null)
               {
                   // Copy notes if available
                   if (!string.IsNullOrWhiteSpace(lastMaint.Notes))
                   {
                       MaintNotesBox.Text = lastMaint.Notes;
                   }
                   
                   // Optional: Pre-fill next due date logic based on previous plan? 
                   // Accessing plans is better done via the service if we had a dedicated "GetNextMaintenanceDate" method.
                   // For now, only Notes as requested.
               }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pre-filling notes: {ex.Message}");
            }
        }
    }

    private async void MaintenanceDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (MaintEquipmentCombo.SelectedItem is EquipmentListDto selectedEq)
        {
            var typeStr = (MaintTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var type = typeStr == "PREVENTIVE" ? Shared.Models.MaintenanceEventType.PREVENTIVE :
                       typeStr == "CORRECTIVE" ? Shared.Models.MaintenanceEventType.CORRECTIVE :
                       Shared.Models.MaintenanceEventType.CALIBRATION;

            var outcome = (MaintOutcomeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Pass";
            var maintDate = MaintDatePicker.Date.DateTime;
            var hasIssues = MaintHasIssuesCheck.IsChecked ?? false;

            // Get next maintenance date
            int? nextMaintMonth = null;
            int? nextMaintYear = null;
            if (NextMaintMonthCombo.SelectedItem is ComboBoxItem monthItem && NextMaintYearCombo.SelectedItem is ComboBoxItem yearItem)
            {
                nextMaintMonth = int.Parse(monthItem.Tag?.ToString() ?? "1");
                nextMaintYear = int.Parse(yearItem.Tag?.ToString() ?? DateTime.Now.Year.ToString());
            }

            var req = new RegisterMaintenanceRequest(
                selectedEq.Id,
                null, // PlanId
                maintDate, // PerformedAt
                type,
                outcome,
                MaintNotesBox.Text,
                null, // EvidenceDocId - removed
                hasIssues,
                nextMaintMonth,
                nextMaintYear,
                _authService.CurrentUserId // UserId
            );

            try
            {
                if (_editingMaintenanceId.HasValue)
                {
                    var updateReq = new UpdateMaintenanceRequest(
                        _editingMaintenanceId.Value,
                        selectedEq.Id,
                        maintDate,
                        type,
                        outcome,
                        MaintNotesBox.Text,
                        hasIssues,
                        nextMaintMonth,
                        nextMaintYear,
                        _authService.CurrentUserId
                    );
                    var updated = await _equipmentService.UpdateMaintenanceAsync(updateReq);
                    if (updated != null)
                    {
                        selectedEq.LastMaintenanceAt = updated.PerformedAt;
                        selectedEq.LastEventType = updated.EventType.ToString();
                        selectedEq.LastOutcome = updated.Outcome;
                        // Calculate next due manually for UI feedback
                         if (nextMaintMonth.HasValue && nextMaintYear.HasValue)
                         {
                             try { selectedEq.NextMaintenanceDue = new DateTime(nextMaintYear.Value, nextMaintMonth.Value, 1).ToString("dd/MM/yyyy"); } catch {}
                         }
                    }
                }
                else
                {
                    var created = await _equipmentService.RegisterMaintenanceAsync(req);
                    if (created != null)
                    {
                        selectedEq.LastMaintenanceEventId = created.Id;
                        selectedEq.LastMaintenanceAt = created.PerformedAt;
                        selectedEq.LastEventType = created.EventType.ToString();
                        selectedEq.LastOutcome = created.Outcome;
                         if (nextMaintMonth.HasValue && nextMaintYear.HasValue)
                         {
                             try { selectedEq.NextMaintenanceDue = new DateTime(nextMaintYear.Value, nextMaintMonth.Value, 1).ToString("dd/MM/yyyy"); } catch {}
                         }
                    }
                }
                
                // Force PropertyChanged notifications if needed (DTO assumes INPC/properties bind automatically if standard objects, 
                // but since it's a DTO we might need to refresh the item in the collection to trigger UI update)
                // Replacing the item in the ObservableCollection triggers a UI refresh for that row.
                var index = Equipment.IndexOf(selectedEq);
                if (index >= 0)
                {
                    Equipment[index] = selectedEq; // Triggers list update
                }
                else
                {
                    await LoadEquipment(); // Fallback
                }

                // Log the action
                var userName = _authService.CurrentUsername ?? "Unknown";
                var actionLabel = _editingMaintenanceId.HasValue ? "Actualizar Mantenimiento" : "Registrar Mantenimiento";
                await _auditLogger.LogEquipmentActionAsync(
                    actionLabel,
                    selectedEq.Name,
                    $"Tipo: {type}, Resultado: {outcome}, Incidencias: {hasIssues}",
                    _authService.CurrentUserId?.ToString(),
                    userName
                );
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Error al procesar mantenimiento: {ex.Message}");
            }
        }
    }


    private async void RegisterQC_Click(object sender, RoutedEventArgs e)
    {
        if (EquipmentList.SelectedItem is EquipmentListDto selected)
        {
             var dialog = new Dialogs.AddDailyQCDialog
             {
                 XamlRoot = this.XamlRoot,
                 Title = $"Registrar QC: {selected.Name}"
             };

             if (await dialog.ShowAsync() == ContentDialogResult.Primary)
             {
                 try
                 {
                     var req = new CreateDailyQCRequest(
                        selected.Id,
                        dialog.LotNumber,
                        dialog.IsPass,
                        dialog.Notes,
                        dialog.PerformedAt,
                        _authService.CurrentUserId // UserId
                     );

                     var success = await _equipmentService.RegisterDailyQCAsync(req);
                     if (success)
                     {
                         await LoadEquipment(); // Refresh to update column
                     }
                     else
                     {
                         await ShowErrorDialog("Error al registrar QC.");
                     }
                 }
                 catch (Exception ex)
                 {
                     await ShowErrorDialog($"Error: {ex.Message}");
                 }
             }
        }
        else
        {
            await ShowErrorDialog("Por favor, seleccione un equipo de la lista para registrar el QC.");
        }
    }

    private void EquipmentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var isSelected = EquipmentList.SelectedItem != null;
        EditEquipmentButton.IsEnabled = isSelected;
        DeleteEquipmentButton.IsEnabled = isSelected;
    }

    private async void EditEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (EquipmentList.SelectedItem is EquipmentListDto selected)
        {
            // Check admin role instead of password
            if (!_authService.IsAdmin)
            {
                await ShowErrorDialog("Acceso denegado. Se requieren permisos de administrador.");
                return;
            }

            var userName = _authService.CurrentUsername ?? "Admin";
            await _auditLogger.LogEquipmentActionAsync(
                "Editar Equipo",
                selected.Name,
                $"ID: {selected.Id}",
                _authService.CurrentUserId?.ToString(),
                userName
            );
            Frame.Navigate(typeof(EquipmentEditorView), selected.Id);
        }
    }

    private async void DeleteEquipment_Click(object sender, RoutedEventArgs e)
    {
        if (EquipmentList.SelectedItem is EquipmentListDto selected)
        {
            // Check admin role instead of password
            if (!_authService.IsAdmin)
            {
                await ShowErrorDialog("Acceso denegado. Se requieren permisos de administrador.");
                return;
            }

            // Confirm deletion
            var confirmMsg = $"¿Está seguro de eliminar el equipo '{selected.Name}' (Etiqueta: {selected.AssetTag})?\nEsta acción es permanente.";
            var confirmDialog = new ContentDialog
            {
                Title = "Confirmar eliminación",
                Content = confirmMsg,
                PrimaryButtonText = "Borrar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            if (await confirmDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    var userName = _authService.CurrentUsername ?? "Admin";
                    await _equipmentService.DeleteEquipmentAsync(selected.Id);
                    await _auditLogger.LogEquipmentActionAsync(
                        "Eliminar Equipo",
                        selected.Name,
                        $"Etiqueta: {selected.AssetTag}",
                        _authService.CurrentUserId?.ToString(),
                        userName
                    );
                    await LoadEquipment();
                    await ShowErrorDialog("Equipo eliminado correctamente.");
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog($"Error al eliminar: {ex.Message}");
                }
            }
        }
    }

    private async Task ShowErrorDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Aviso",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void Sort_AssetTag_Click(object sender, RoutedEventArgs e)
    {
        SortList(x => x.AssetTag);
    }
    private void Sort_Name_Click(object sender, RoutedEventArgs e)
    {
         SortList(x => x.Name);
    }
    private void Sort_Location_Click(object sender, RoutedEventArgs e)
    {
         SortList(x => x.Location);
    }

    private void SortList<TKey>(Func<EquipmentListDto, TKey> keySelector)
    {
        var sorted = System.Linq.Enumerable.OrderBy(Equipment, keySelector).ToList();
        Equipment.Clear();
        foreach (var i in sorted) Equipment.Add(i);
    }

    private async void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exportService = ((App)Application.Current).ExportService;
            await exportService.ExportEquipmentToExcelAsync(Equipment);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export error: {ex.Message}");
        }
    }
}

