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
    private List<ReagentType> _types = new();

    public ConfigurationView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadData();
    }

    private async Task LoadData()
    {
        var service = ((App)Application.Current).ConfigurationService;
        
        // Load Reagent Types
        var types = await service.GetReagentTypesAsync();
        _types = new List<ReagentType>(types);
        ReagentTypesList.ItemsSource = _types;

        // Load Document Settings
        var setting = await service.GetSettingAsync("DefaultDocumentPath");
        if (setting != null)
        {
            DefaultPathBox.Text = setting.Value;
        }

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
    }

    // Reagent Types Management
    
    private async void AddReagentType_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewTypeBox.Text)) return;

        var service = ((App)Application.Current).ConfigurationService;
        var newType = new ReagentType { Name = NewTypeBox.Text };
        
        var result = await service.CreateReagentTypeAsync(newType);
        if (result != null)
        {
            NewTypeBox.Text = "";
            await LoadData();
        }
    }

    private async void DeleteType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
             var service = ((App)Application.Current).ConfigurationService;
             if (await service.DeleteReagentTypeAsync(id))
             {
                 await LoadData();
             }
        }
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
            
            StatusText.Text = "⏳ Sincronización en progreso...";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue);
            
            // Ejecutar sync manual
            await app.DriveSyncEngine.RunSyncAsync();
            
            StatusText.Text = "✅ Sincronización completa finalizada.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error en sincronización: {ex.Message}";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }

    private void ViewSyncLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QMSFlowDoc", "Logs");
            
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

    // Document Settings
    
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var service = ((App)Application.Current).ConfigurationService;
        var setting = new SystemSetting 
        { 
            Key = "DefaultDocumentPath", 
            Value = DefaultPathBox.Text,
            Description = "Ruta o ID de carpeta por defecto"
        };
        
        if (await service.UpdateSettingAsync(setting))
        {
            StatusText.Text = "Configuración guardada.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        else
        {
            StatusText.Text = "Error al guardar.";
            StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }
}
