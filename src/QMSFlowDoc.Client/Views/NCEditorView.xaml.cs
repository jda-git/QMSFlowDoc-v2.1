using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;

namespace QMSFlowDoc.Client.Views;

public sealed partial class NCEditorView : Page
{
    private Guid? _ncId;

    public NCEditorView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            _ncId = id;
            await LoadNC(id);
        }
    }

    private async System.Threading.Tasks.Task LoadNC(Guid id)
    {
        try
        {
            var service = ((App)Application.Current).QualityService;
            var nc = await service.GetNCByIdAsync(id);
            if (nc != null)
            {
                TitleBox.Text = nc.Title;
                DescriptionBox.Text = nc.Description;
                ContainmentBox.Text = nc.Containment;
                ImpactPatientCheck.IsChecked = nc.ImpactPatient;

                // Select Severity
                foreach (ComboBoxItem item in SeverityCombo.Items)
                {
                    if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tagVal) && tagVal == (int)nc.Severity)
                    {
                        SeverityCombo.SelectedItem = item;
                        break;
                    }
                }

                // Select Status
                foreach (ComboBoxItem item in StatusCombo.Items)
                {
                    if (item.Content.ToString() == nc.Status.ToString())
                    {
                        StatusCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
             ErrorText.Text = $"Error loading NC: {ex.Message}";
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
        
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            ErrorText.Text = "El título es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var severity = (NCSeverity)(int.TryParse((SeverityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out int sev) ? sev : 1);
            var statusStr = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            NCStatus? status = null;
            if (Enum.TryParse<NCStatus>(statusStr, out var sEnum)) status = sEnum;

            var request = new CreateNCRequest(
                TitleBox.Text,
                DescriptionBox.Text,
                severity,
                status,
                ImpactPatientCheck.IsChecked ?? false,
                ContainmentBox.Text
            );

            var service = ((App)Application.Current).QualityService;

            if (_ncId.HasValue)
            {
                // Update
                var success = await service.UpdateNCAsync(_ncId.Value, request);
                
                // Update Status
                statusStr = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (Enum.TryParse<NCStatus>(statusStr, out var statusEnum))
                {
                    await service.UpdateNCStatusAsync(_ncId.Value, (int)statusEnum);
                }

                if (success) Frame.GoBack();
                else { ErrorText.Text = "Error al actualizar."; ErrorText.Visibility = Visibility.Visible; }
            }
            else
            {
                // Create
                var result = await service.CreateNCAsync(request);
                if (result != null)
                {
                    Frame.GoBack();
                }
                else
                {
                    ErrorText.Text = "Error al enviar el reporte.";
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
