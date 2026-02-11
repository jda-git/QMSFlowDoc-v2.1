using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ConfigurationView : Page
{
    public ConfigurationView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // Check permissions
        var app = (App)Application.Current;
        if (app.AuthService.IsAdmin)
        {
            SecurityPanel.Visibility = Visibility.Visible;
        }
        else
        {
            SecurityPanel.Visibility = Visibility.Collapsed;
        }

        await LoadData();
    }

    private void ManagePermissions_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(PermissionsConfigView));
    }

    private async Task LoadData()
    {
        var service = ((App)Application.Current).ConfigurationService;
        
        // Load Network Storage Configuration
        var app = (App)Application.Current;
        var networkPath = await app.NetworkConfigStore.GetNetworkBasePathAsync();
        var localPath = await app.NetworkConfigStore.GetLocalBasePathAsync();
        
        if (!string.IsNullOrWhiteSpace(networkPath))
        {
            NetworkPathBox.Text = networkPath;
        }
        
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            LocalPathBox.Text = localPath;
        }
        
        // Load advanced settings
        var config = await app.NetworkConfigStore.LoadAsync();
        AutoSyncCheckBox.IsChecked = config.AutoSyncOnStartup;
        SyncIntervalSlider.Value = config.SyncIntervalMinutes;
        InactivitySlider.Value = config.InactivityTimeoutMinutes > 0 ? config.InactivityTimeoutMinutes : 30;
    }

    // Network Storage Configuration

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NetworkPathBox.Text))
        {
            StatusText.Text = "Por favor, introduce una ruta de red válida.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            return;
        }

        try
        {
            var provider = new Services.Storage.NetworkStorageProvider(NetworkPathBox.Text);
            var success = await provider.TestConnectionAsync();
            
            if (success)
            {
                StatusText.Text = "✅ Conexión exitosa. La ruta de red es accesible y tiene permisos read/write.";
                StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error de conexión: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }

    private async void InitializeStructure_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NetworkPathBox.Text) || string.IsNullOrWhiteSpace(LocalPathBox.Text))
        {
            StatusText.Text = "Por favor, configura ambas rutas (red y local) primero.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            return;
        }

        try
        {
            var app = (App)Application.Current;
            
            // Guardar rutas temporalmente
            await app.NetworkConfigStore.SetNetworkBasePathAsync(NetworkPathBox.Text);
            await app.NetworkConfigStore.SetLocalBasePathAsync(LocalPathBox.Text);
            
            // Inicializar estructura QMS/
            await app.NetworkConfigStore.InitializeStructureAsync();
            
            StatusText.Text = "✅ Estructura QMS inicializada correctamente en red y local.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error al inicializar: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }

    private async void SaveNetworkSettings_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NetworkPathBox.Text) || string.IsNullOrWhiteSpace(LocalPathBox.Text))
        {
            StatusText.Text = "Por favor, completa ambas rutas.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            return;
        }

        try
        {
            var app = (App)Application.Current;
            var config = await app.NetworkConfigStore.LoadAsync();
            
            config.NetworkBasePath = NetworkPathBox.Text;
            config.LocalBasePath = LocalPathBox.Text;
            config.AutoSyncOnStartup = AutoSyncCheckBox.IsChecked ?? true;
            config.SyncIntervalMinutes = (int)SyncIntervalSlider.Value;
            config.InactivityTimeoutMinutes = (int)InactivitySlider.Value;
            
            await app.NetworkConfigStore.SaveAsync(config);
            
            StatusText.Text = "✅ Configuración guardada. Reinicia la aplicación para aplicar cambios.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error al guardar: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }

    private async void ForceSync_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = (App)Application.Current;
            
            // Verificar configuración
            if (!await app.NetworkConfigStore.ValidatePathsAsync())
            {
                StatusText.Text = "❌ Rutas de red/local no configuradas.";
                StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                return;
            }
            
            StatusText.Text = "⏳ Sincronización en progreso...";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue);
            
            // Ejecutar sync manual usando NetworkSyncService
            await app.NetworkSyncService.SyncAllAsync();
            
            StatusText.Text = "✅ Sincronización completa finalizada.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error en sincronización: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }

    private async void ViewSyncLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = (App)Application.Current;
            var localPath = await app.NetworkConfigStore.GetLocalBasePathAsync();
            
            string logPath;
            if (!string.IsNullOrEmpty(localPath))
            {
                logPath = Path.Combine(localPath, "Base_datos", "Logs");
            }
            else
            {
                // Fallback to AppData if not configured
                logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QMSFlowDoc", "Logs");
            }
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(logPath);
            
            // Abrir carpeta de logs en Explorer
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true,
                Verb = "open"
            });
            
            StatusText.Text = "Carpeta de logs abierta.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error al abrir logs: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }

    private async void ExportAuditLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "⏳ Generando reporte...";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue);

            // Fetch last 1000 logs
            var app = (App)Application.Current;
            var logs = await app.LocalStore.GetAuditLogsAsync();

            var exportService = app.ExportService;
            await exportService.ExportAuditLogToPdfAsync(logs);

            StatusText.Text = "✅ Reporte de auditoría exportado.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error al exportar: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }
}
