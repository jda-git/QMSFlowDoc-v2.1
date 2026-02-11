using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ComplaintsView : Page
{
    public ObservableCollection<ComplaintListDto> Complaints { get; } = new();
    private List<ComplaintListDto> _allComplaints = new();

    public ComplaintsView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadComplaints();
    }

    private async Task LoadComplaints()
    {
        try
        {
            var store = ((App)Application.Current).LocalStore;
            _allComplaints = await store.GetComplaintsAsync();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading complaints: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        Complaints.Clear();
        var filter = (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();

        foreach (var c in _allComplaints)
        {
            if (filter == "Abiertas" && c.Status != ComplaintStatus.OPEN) continue;
            if (filter == "En Investigación" && c.Status != ComplaintStatus.INVESTIGATING) continue;
            if (filter == "Cerradas" && c.Status != ComplaintStatus.CLOSED) continue;
            Complaints.Add(c);
        }
    }

    private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_allComplaints.Count > 0 || StatusFilter.SelectedItem != null) ApplyFilter();
    }

    private async void AddComplaint_Click(object sender, RoutedEventArgs e)
    {
        var stack = new StackPanel { Spacing = 10 };
        var sourceBox = new TextBox { Header = "Origen de la Queja", PlaceholderText = "Nombre del paciente, clínico, etc." };
        var descBox = new TextBox
        {
            Header = "Descripción",
            PlaceholderText = "Describa la queja en detalle",
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 80
        };
        var categoryBox = new ComboBox
        {
            Header = "Categoría",
            Items =
            {
                new ComboBoxItem { Content = "Paciente", Tag = ComplaintCategory.PATIENT },
                new ComboBoxItem { Content = "Clínico", Tag = ComplaintCategory.CLINICAL },
                new ComboBoxItem { Content = "Tiempo de Respuesta", Tag = ComplaintCategory.TURNAROUND },
                new ComboBoxItem { Content = "Error en Informe", Tag = ComplaintCategory.REPORT_ERROR },
                new ComboBoxItem { Content = "Otro", Tag = ComplaintCategory.OTHER }
            },
            SelectedIndex = 0,
            MinWidth = 200
        };

        stack.Children.Add(sourceBox);
        stack.Children.Add(descBox);
        stack.Children.Add(categoryBox);

        var dialog = new ContentDialog
        {
            Title = "Registrar Queja (ISO 15189 §7.7)",
            Content = stack,
            PrimaryButtonText = "Registrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(sourceBox.Text) || string.IsNullOrWhiteSpace(descBox.Text))
            {
                var errDlg = new ContentDialog { Title = "Error", Content = "Origen y Descripción son obligatorios.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await errDlg.ShowAsync();
                return;
            }

            var category = (ComplaintCategory?)((categoryBox.SelectedItem as ComboBoxItem)?.Tag) ?? ComplaintCategory.OTHER;
            var store = ((App)Application.Current).LocalStore;
            var req = new CreateComplaintRequest(
                sourceBox.Text.Trim(),
                descBox.Text.Trim(),
                category,
                null,
                null
            );
            await store.CreateComplaintAsync(req);
            await LoadComplaints();
        }
    }

    private async void ComplaintsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ComplaintListDto complaint)
        {
            // Show status change dialog
            var statusBox = new ComboBox
            {
                Header = "Actualizar Estado",
                Items =
                {
                    new ComboBoxItem { Content = "Abierta", Tag = ComplaintStatus.OPEN },
                    new ComboBoxItem { Content = "En Investigación", Tag = ComplaintStatus.INVESTIGATING },
                    new ComboBoxItem { Content = "Cerrada", Tag = ComplaintStatus.CLOSED }
                },
                SelectedIndex = (int)complaint.Status,
                MinWidth = 200
            };

            var dialog = new ContentDialog
            {
                Title = $"Queja: {complaint.Source}",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = complaint.Description, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
                        new TextBlock { Text = $"Categoría: {complaint.Category}", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) },
                        statusBox
                    }
                },
                PrimaryButtonText = "Actualizar",
                CloseButtonText = "Cerrar",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var newStatus = (ComplaintStatus?)((statusBox.SelectedItem as ComboBoxItem)?.Tag) ?? complaint.Status;
                if (newStatus != complaint.Status)
                {
                    var store = ((App)Application.Current).LocalStore;
                    await store.UpdateComplaintStatusAsync(complaint.Id, newStatus);
                    await LoadComplaints();
                }
            }
        }
    }
}
