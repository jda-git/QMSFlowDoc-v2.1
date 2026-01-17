using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;

namespace QMSFlowDoc.Client.Views;

public sealed partial class AuditView : Page
{
    private readonly IAuditService _auditService;

    public AuditView()
    {
        this.InitializeComponent();
        _auditService = App.Services.GetRequiredService<IAuditService>();
        Loaded += AuditView_Loaded;
    }

    private async void AuditView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            LoadingBar.Visibility = Visibility.Visible;
            AuditGrid.Visibility = Visibility.Collapsed;
            EmptyStateText.Visibility = Visibility.Collapsed;

            var filter = new AuditFilter
            {
                FromDate = StartDatePicker.Date?.DateTime,
                ToDate = EndDatePicker.Date?.DateTime,
                UserName = string.IsNullOrWhiteSpace(UserFilterBox.Text) ? null : UserFilterBox.Text,
                Action = string.IsNullOrWhiteSpace(ActionFilterBox.Text) ? null : ActionFilterBox.Text
            };

            var logs = await _auditService.GetLogsAsync(filter);
            AuditGrid.ItemsSource = logs;

            if (logs.Count == 0)
            {
                EmptyStateText.Visibility = Visibility.Visible;
            }
            else
            {
                AuditGrid.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
             var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Error al cargar auditoría: {ex.Message}",
                CloseButtonText = "Ok",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void ApplyFilters_Click(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        StartDatePicker.Date = null;
        EndDatePicker.Date = null;
        UserFilterBox.Text = string.Empty;
        ActionFilterBox.Text = string.Empty;
        await LoadDataAsync();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var logs = AuditGrid.ItemsSource as List<AuditLogDto>;
        if (logs == null || !logs.Any()) return;

        var sb = new StringBuilder();
        sb.AppendLine("Fecha,Usuario,Acción,Entidad,ID Entidad,Detalles");
        foreach (var log in logs)
        {
            sb.AppendLine($"{log.Timestamp:O},{Escape(log.UserName)},{Escape(log.Action)},{Escape(log.EntityType)},{Escape(log.EntityId)},{Escape(log.Details)}");
        }

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("CSV File", new List<string>() { ".csv" });
        savePicker.SuggestedFileName = $"AuditLog_{DateTime.Now:yyyyMMdd}";
        
        // WinUI3 workaround for SavePicker
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
            
             var dialog = new ContentDialog
            {
                Title = "Exportación Exitosa",
                Content = $"Archivo guardado en {file.Path}",
                CloseButtonText = "Ok",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private string Escape(string? s)
    {
        if (s == null) return "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
        {
            return $"\"{s.Replace("\"", "\"\"")}\"";
        }
        return s;
    }
}
