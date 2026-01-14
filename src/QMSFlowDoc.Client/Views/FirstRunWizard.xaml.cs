using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class FirstRunWizard : Page
{
    private int _currentPage = 1;
    private bool _networkPathValid = false;
    private bool _localPathValid = false;

    public FirstRunWizard()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        UpdatePageVisibility();
    }

    private void UpdatePageVisibility()
    {
        // Hide all pages
        Page1.Visibility = Visibility.Collapsed;
        Page2.Visibility = Visibility.Collapsed;
        Page3.Visibility = Visibility.Collapsed;
        Page4.Visibility = Visibility.Collapsed;

        // Show current page
        switch (_currentPage)
        {
            case 1:
                Page1.Visibility = Visibility.Visible;
                StepTitle.Text = "Bienvenido";
                BackButton.IsEnabled = false;
                NextButton.Visibility = Visibility.Visible;
                FinishButton.Visibility = Visibility.Collapsed;
                break;
            case 2:
                Page2.Visibility = Visibility.Visible;
                StepTitle.Text = "Ruta de Red";
                BackButton.IsEnabled = true;
                NextButton.IsEnabled = _networkPathValid;
                break;
            case 3:
                Page3.Visibility = Visibility.Visible;
                StepTitle.Text = "Ruta Local";
                NextButton.IsEnabled = _localPathValid;
                break;
            case 4:
                Page4.Visibility = Visibility.Visible;
                StepTitle.Text = "Inicialización";
                NextButton.Visibility = Visibility.Collapsed;
                FinishButton.Visibility = Visibility.Visible;
                FinishButton.IsEnabled = false; // Will enable after successful init
                
                // Update summary
                SummaryNetworkPath.Text = NetworkPathBox.Text;
                SummaryLocalPath.Text = LocalPathBox.Text;
                break;
        }

        ProgressText.Text = $"Paso {_currentPage} de 4";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            UpdatePageVisibility();
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage < 4)
        {
            _currentPage++;
            UpdatePageVisibility();
        }
    }

    private async void Finish_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = (App)Application.Current;
            var config = await app.NetworkConfigStore.LoadAsync();
            
            config.NetworkBasePath = NetworkPathBox.Text;
            config.LocalBasePath = LocalPathBox.Text;
            config.UseNetworkStorage = true;
            config.AutoSyncOnStartup = true;
            config.SyncIntervalMinutes = 5;
            config.LastSyncAt = DateTime.UtcNow;
            
            await app.NetworkConfigStore.SaveAsync(config);
            
            // Navigate to main window
            if (Window.Current.Content is Frame rootFrame)
            {
                rootFrame.Navigate(typeof(MainWindow));
            }
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Error al guardar configuración: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    // Network Path Validation

    private void NetworkPathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _networkPathValid = false;
        NetworkTestResult.Visibility = Visibility.Collapsed;
        if (_currentPage == 2)
        {
            NextButton.IsEnabled = false;
        }
    }

    private async void TestNetworkPath_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NetworkPathBox.Text))
        {
            ShowNetworkResult(false, "Por favor, introduce una ruta válida.");
            return;
        }

        NetworkTestProgress.IsActive = true;
        NetworkTestResult.Visibility = Visibility.Collapsed;

        try
        {
            var provider = new Services.Storage.NetworkStorageProvider(NetworkPathBox.Text);
            var success = await provider.TestConnectionAsync();

            NetworkTestProgress.IsActive = false;

            if (success)
            {
                _networkPathValid = true;
                ShowNetworkResult(true, "✅ Conexión exitosa. La ruta es accesible y tiene permisos lectura/escritura.");
                if (_currentPage == 2)
                {
                    NextButton.IsEnabled = true;
                }
            }
        }
        catch (Exception ex)
        {
            NetworkTestProgress.IsActive = false;
            _networkPathValid = false;
            ShowNetworkResult(false, $"❌ Error: {ex.Message}");
        }
    }

    private void ShowNetworkResult(bool success, string message)
    {
        NetworkTestResult.Text = message;
        NetworkTestResult.Foreground = new SolidColorBrush(success ? Colors.Green : Colors.Red);
        NetworkTestResult.Visibility = Visibility.Visible;
    }

    // Local Path Validation

    private void LocalPathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _localPathValid = false;
        LocalTestResult.Visibility = Visibility.Collapsed;
        if (_currentPage == 3)
        {
            NextButton.IsEnabled = false;
        }
    }

    private async void ValidateLocalPath_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LocalPathBox.Text))
        {
            ShowLocalResult(false, "Por favor, introduce una ruta válida.");
            return;
        }

        LocalTestProgress.IsActive = true;
        LocalTestResult.Visibility = Visibility.Collapsed;

        await Task.Delay(500); // Simulate validation

        try
        {
            var path = LocalPathBox.Text;
            
            // Check if path is valid
            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException("La ruta debe ser absoluta (ej: C:\\...)");
            }

            // Try to create directory
            Directory.CreateDirectory(path);

            // Check write permissions
            var testFile = Path.Combine(path, $".qms_test_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            LocalTestProgress.IsActive = false;
            _localPathValid = true;
            ShowLocalResult(true, "✅ Ruta válida con permisos de escritura.");
            
            if (_currentPage == 3)
            {
                NextButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            LocalTestProgress.IsActive = false;
            _localPathValid = false;
            ShowLocalResult(false, $"❌ Error: {ex.Message}");
        }
    }

    private void ShowLocalResult(bool success, string message)
    {
        LocalTestResult.Text = message;
        LocalTestResult.Foreground = new SolidColorBrush(success ? Colors.Green : Colors.Red);
        LocalTestResult.Visibility = Visibility.Visible;
    }

    // Initialization

    private async void Initialize_Click(object sender, RoutedEventArgs e)
    {
        InitializeButton.IsEnabled = false;
        InitProgress.Visibility = Visibility.Visible;
        InitProgress.IsIndeterminate = true;
        InitStatusText.Visibility = Visibility.Visible;

        try
        {
            var app = (App)Application.Current;

            InitStatusText.Text = "⏳ Guardando configuración...";
            await app.NetworkConfigStore.SetNetworkBasePathAsync(NetworkPathBox.Text);
            await app.NetworkConfigStore.SetLocalBasePathAsync(LocalPathBox.Text);

            InitStatusText.Text = "⏳ Creando estructura de carpetas QMS...";
            await Task.Delay(500);
            await app.NetworkConfigStore.InitializeStructureAsync();

            InitStatusText.Text = "⏳ Inicializando base de datos de sincronización...";
            await Task.Delay(300);
            await app.SnapshotStore.InitializeAsync();

            InitProgress.IsIndeterminate = false;
            InitProgress.Value = 100;
            InitStatusText.Text = "✅ Inicialización completada correctamente.";
            InitStatusText.Foreground = new SolidColorBrush(Colors.Green);

            FinishButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            InitProgress.Visibility = Visibility.Collapsed;
            InitStatusText.Text = $"❌ Error: {ex.Message}";
            InitStatusText.Foreground = new SolidColorBrush(Colors.Red);
            InitializeButton.IsEnabled = true;
        }
    }
}
