using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class EquipmentEditorView : Page
{
    private Guid? _equipmentId = null;
    private readonly Services.IEquipmentService _equipmentService;
    private readonly Services.IAuditLogger _auditLogger;
    private readonly Services.IAuthService _authService;

    public EquipmentEditorView()
    {
        this.InitializeComponent();
        _equipmentService = ((App)Application.Current).EquipmentService;
        _auditLogger = ((App)Application.Current).EquipmentAuditLogger;
        _authService = ((App)Application.Current).AuthService;
        InstallationDatePicker.Date = DateTimeOffset.Now;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is Guid equipmentId)
        {
            _equipmentId = equipmentId;
            await LoadEquipment(equipmentId);
        }
    }

    private async Task LoadEquipment(Guid id)
    {
        try
        {
            var equipment = await _equipmentService.GetEquipmentByIdAsync(id);
            if (equipment != null)
            {
                AssetTagBox.Text = equipment.AssetTag ?? "";
                NameBox.Text = equipment.Name;
                ManufacturerBox.Text = equipment.Manufacturer ?? "";
                ModelBox.Text = equipment.Model ?? "";
                SerialBox.Text = equipment.SerialNumber ?? "";
                LocationBox.Text = equipment.Location ?? "";
                if (equipment.InstalledAt.HasValue)
                {
                    InstallationDatePicker.Date = new DateTimeOffset(equipment.InstalledAt.Value);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error al cargar equipo: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "El nombre del equipo es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            Equipment? result;
            
            if (_equipmentId.HasValue)
            {
                // UPDATE existing equipment
                var updateRequest = new UpdateEquipmentRequest(
                    _equipmentId.Value,
                    AssetTagBox.Text,
                    NameBox.Text,
                    ManufacturerBox.Text,
                    ModelBox.Text,
                    SerialBox.Text,
                    null, // SoftwareVersion
                    null, // FirmwareVersion
                    LocationBox.Text,
                    InstallationDatePicker.Date.UtcDateTime
                );
                result = await _equipmentService.UpdateEquipmentAsync(updateRequest);
            }
            else
            {
                // CREATE new equipment
                var createRequest = new CreateEquipmentRequest(
                    AssetTagBox.Text,
                    NameBox.Text,
                    ManufacturerBox.Text,
                    ModelBox.Text,
                    SerialBox.Text,
                    null, // SoftwareVersion
                    null, // FirmwareVersion
                    LocationBox.Text,
                    InstallationDatePicker.Date.UtcDateTime
                );
                result = await _equipmentService.CreateEquipmentAsync(createRequest);
            }

            if (result != null)
            {
                // Log the action
                var userName = _authService.CurrentUsername ?? "Unknown";
                var action = _equipmentId.HasValue ? "Actualizar Equipo" : "Crear Equipo";
                await _auditLogger.LogEquipmentActionAsync(
                    action,
                    NameBox.Text,
                    $"Etiqueta: {AssetTagBox.Text}, Fabricante: {ManufacturerBox.Text}",
                    _authService.CurrentUserId?.ToString(),
                    userName
                );
                Frame.GoBack();
            }
            else
            {
                ErrorText.Text = "Error al guardar el equipo.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
