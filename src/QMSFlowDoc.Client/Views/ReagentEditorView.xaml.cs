using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using System;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ReagentEditorView : Page
{
    private Guid? _reagentId;
    private QMSFlowDoc.Shared.Models.Reagent? _existingReagent;

    public ReagentEditorView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is Guid id)
        {
            _reagentId = id;
            await LoadReagent(id);
        }
        else
        {
            await LoadTypes(null);
        }
    }

    private async System.Threading.Tasks.Task LoadTypes(string? selectedType)
    {
        try 
        {
           var configService = ((App)Application.Current).ConfigurationService;
           var types = await configService.GetReagentTypesAsync();
           
           TypeCombo.Items.Clear();
           foreach (var t in types)
           {
               var item = new ComboBoxItem { Content = t.Name, Tag = t.Id };
               TypeCombo.Items.Add(item);
               
               if (selectedType != null && t.Name == selectedType)
               {
                   TypeCombo.SelectedItem = item;
               }
           }
           
           if (TypeCombo.SelectedItem == null && TypeCombo.Items.Count > 0)
               TypeCombo.SelectedIndex = 0;
        }
        catch { /* Ignore or Log */ }
    }

    private async System.Threading.Tasks.Task LoadReagent(Guid id)
    {
        try
        {
            var service = ((App)Application.Current).InventoryService;
            var reagent = await service.GetReagentByIdAsync(id);
            if (reagent != null)
            {
                _existingReagent = reagent;
                NameBox.Text = reagent.Name;
                ManufacturerBox.Text = reagent.Manufacturer;
                InternalCodeBox.Text = reagent.InternalCode;
                FluorescenceBox.Text = reagent.Fluorescence;
                ReferenceBox.Text = reagent.Reference;
                MinStockBox.Value = (double)reagent.MinStock;
                TargetStockBox.Value = (double)reagent.TargetStock;

                // Load Types dynamically
                await LoadTypes(reagent.ReagentType);

                // Select Status
                foreach (ComboBoxItem item in StatusCombo.Items)
                {
                    if (item.Content.ToString() == reagent.Status.ToString())
                    {
                        StatusCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
             ErrorText.Text = $"Error cargando reactivo: {ex.Message}";
             ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void ManageTypes_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.ManageReagentTypesDialog();
        dialog.XamlRoot = this.Content.XamlRoot;
        await dialog.ShowAsync();
        
        // Refresh ComboBox
        string? currentText = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        await LoadTypes(currentText);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "El nombre del reactivo es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var type = (TypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Químico";
            
            var request = new CreateReagentRequest(
                NameBox.Text,
                ManufacturerBox.Text,
                null, // SupplierId
                null, // ManufacturerCode
                InternalCodeBox.Text,
                FluorescenceBox.Text,
                type,
                ReferenceBox.Text ?? "Unidad",
                null, // Classification (ISO 15189)
                null, // StorageConditions
                null, // DefaultLocationId
                null, // OpenShelfLifeDays
                (decimal)MinStockBox.Value,
                (decimal)TargetStockBox.Value,
                (decimal)TargetStockBox.Value // ReorderQty = Target by default
            );

            var service = ((App)Application.Current).InventoryService;

            if (_reagentId.HasValue)
            {
                // Update
                var success = await service.UpdateReagentAsync(_reagentId.Value, request);
                
                // Update Status separately
                var statusStr = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (Enum.TryParse<QMSFlowDoc.Shared.Models.ReagentStatus>(statusStr, out var statusEnum))
                {
                    await service.UpdateReagentStatusAsync(_reagentId.Value, (int)statusEnum);
                }

                if (success) Frame.GoBack();
                else { ErrorText.Text = "Error al actualizar."; ErrorText.Visibility = Visibility.Visible; }
            }
            else
            {
                // Create
                var result = await service.CreateReagentAsync(request);
                if (result != null)
                {
                    Frame.GoBack();
                }
                else
                {
                    ErrorText.Text = "Error al guardar el reactivo.";
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
}
