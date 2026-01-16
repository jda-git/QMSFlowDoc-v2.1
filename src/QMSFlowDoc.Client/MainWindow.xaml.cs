using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Client.Views.Dialogs;

namespace QMSFlowDoc.Client;

public sealed partial class MainWindow : Window
{
    private readonly ISearchService _searchService;
    private readonly IAuthService _authService;
    private System.Threading.Timer? _statusUpdateTimer;

    private DateTime _lastActivity = DateTime.Now;
    private System.Threading.Timer? _inactivityTimer;

    public MainWindow()
    {
        this.InitializeComponent();
        _authService = ((App)Application.Current).AuthService;
        _searchService = ((App)Application.Current).SearchService;
        
        var syncEngine = ((App)Application.Current).DriveSyncEngine;
        syncEngine.SyncStatusChanged += SyncEngine_SyncStatusChanged;
        
        // Start periodic status updates (every 10 seconds)
        _statusUpdateTimer = new System.Threading.Timer(
            async _ => await UpdateSyncStatusAsync(),
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10));

        // Start inactivity monitor (check every 1 minute)
        _inactivityTimer = new System.Threading.Timer(
            CheckInactivity,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
            
        // Hook global events for activity tracking
        this.Content.PointerMoved += (s, e) => ResetInactivity();
        this.Content.KeyDown += (s, e) => ResetInactivity();
        this.Content.PointerPressed += (s, e) => ResetInactivity();
    }

    private void ResetInactivity()
    {
        _lastActivity = DateTime.Now;
    }

    private async void CheckInactivity(object? state)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => CheckInactivity(state));
            return;
        }

        if (!_authService.IsAuthenticated) return;

        var app = (App)Application.Current;
        var config = await app.NetworkConfigStore.LoadAsync();
        var timeoutMinutes = config.InactivityTimeoutMinutes > 0 ? config.InactivityTimeoutMinutes : 30;

        if ((DateTime.Now - _lastActivity).TotalMinutes >= timeoutMinutes)
        {
            PerformLogout("Sesión cerrada por inactividad.");
        }
    }

    private void Logout_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        PerformLogout("Sesión cerrada correctamente.");
    }

    private void PerformLogout(string message)
    {
        _authService.Logout();
        // Reset timer or just let it run (it checks IsAuthenticated)
        
        ShowLogin();
        
        // Show feedback on Login page (optional, or just navigate)
        // Ideally pass parameter to LoginView but avoiding complexity for now
    }


    private void SyncEngine_SyncStatusChanged(string status)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            SyncStatusText.Text = status;
        });
    }

    private async System.Threading.Tasks.Task UpdateSyncStatusAsync()
    {
        try
        {
            var app = (App)Application.Current;
            
            // Get pending operations count
            var pendingCount = await app.NetworkConfigStore.LoadAsync();
            
            // Get conflicts count
            var conflictsCount = (await app.SnapshotStore.GetConflictsAsync()).Count;
            
            // Update UI on dispatcher thread
            this.DispatcherQueue.TryEnqueue(() =>
            {
                // Update pending ops badge
                if (pendingCount != null)
                {
                    PendingOpsButton.Visibility = Visibility.Collapsed; // Will be visible when OfflineQueue is integrated
                }
                
                // Update conflicts badge
                if (conflictsCount > 0)
                {
                    ConflictsButton.Visibility = Visibility.Visible;
                    ConflictsCount.Text = conflictsCount.ToString();
                }
                else
                {
                    ConflictsButton.Visibility = Visibility.Collapsed;
                }
                
                // Update last sync time (placeholder - will be real when SyncScheduler is integrated)
                LastSyncTimeText.Text = "Última sincronización: Hace unos momentos";
            });
        }
        catch
        {
            // Silently fail - don't crash UI for status updates
        }
    }

    private async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Disable button during sync
            SyncButton.IsEnabled = false;
            SyncStatusText.Text = "Sincronizando...";
            SyncStatusText.Foreground = new SolidColorBrush(Colors.Blue);
            
            var syncEngine = ((App)Application.Current).DriveSyncEngine;
            await syncEngine.RunSyncAsync();
            
            SyncStatusText.Text = "Sincronizado";
            SyncStatusText.Foreground = new SolidColorBrush(Colors.Green);
            
            // Update status immediately after sync
            await UpdateSyncStatusAsync();
        }
        catch (Exception ex)
        {
            SyncStatusText.Text = $"Error: {ex.Message}";
            SyncStatusText.Foreground = new SolidColorBrush(Colors.Red);
        }
        finally
        {
            SyncButton.IsEnabled = true;
        }
    }

    private void PendingOps_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to a view showing pending operations (future implementation)
        var dialog = new ContentDialog
        {
            Title = "Operaciones Pendientes",
            Content = "Hay operaciones pendientes de sincronización. Estas se procesarán automáticamente cuando la red esté disponible.",
            CloseButtonText = "Entendido",
            XamlRoot = this.Content.XamlRoot
        };
        _ = dialog.ShowAsync();
    }

    private void Conflicts_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to conflicts view (future: ConflictsView)
        var dialog = new ContentDialog
        {
            Title = "Conflictos Detectados",
            Content = "Se han detectado archivos con conflictos de sincronización. Ve a Configuración para revisar y resolver los conflictos.",
            PrimaryButtonText = "Ir a Configuración",
            CloseButtonText = "Más tarde",
            XamlRoot = this.Content.XamlRoot
        };
        dialog.PrimaryButtonClick += (_, _) =>
        {
            ContentFrame.Navigate(typeof(Views.ConfigurationView));
        };
        _ = dialog.ShowAsync();
    }

    private async void GlobalSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text;
            if (query.Length > 2)
            {
                var results = await _searchService.SearchAsync(query);
                sender.ItemsSource = results;
            }
            else
            {
                sender.ItemsSource = null;
            }
        }
    }

    private void GlobalSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchResultDto result)
        {
            NavigateToTag(result.Route);
        }
    }

    public void ShowLogin()
    {
        RootNavigationView.Visibility = Visibility.Collapsed;
        RootFrame.Visibility = Visibility.Visible;
        RootFrame.Navigate(typeof(Views.LoginView));
    }

    public void ShowMain()
    {
        RootFrame.Visibility = Visibility.Collapsed;
        RootFrame.Content = null;
        RootNavigationView.Visibility = Visibility.Visible;
        RootNavigationView.SelectedItem = RootNavigationView.MenuItems[0];
        RootNavigationView.SelectionChanged += RootNavigationView_SelectionChanged;
        NavigateToTag("dashboard");
    }

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(Views.ConfigurationView));
        }
        else if (args.SelectedItemContainer != null)
        {
            var tag = args.SelectedItemContainer.Tag.ToString();
            NavigateToTag(tag!);
        }
    }

    private void NavigateToTag(string tag)
    {
        switch (tag)
        {
            case "dashboard":
                ContentFrame.Navigate(typeof(Views.DashboardView));
                break;
            case "documents":
                ContentFrame.Navigate(typeof(Views.DocumentsView));
                break;
            case "inventory":
                ContentFrame.Navigate(typeof(Views.InventoryView));
                break;
            case "equipment":
                ContentFrame.Navigate(typeof(Views.EquipmentView));
                break;
            case "personal":
                ContentFrame.Navigate(typeof(Views.StaffView));
                break;
            case "eqa":
                ContentFrame.Navigate(typeof(Views.EQAView));
                break;
            case "methods":
                ContentFrame.Navigate(typeof(Views.MethodsView));
                break;
            case "improvement":

                ContentFrame.Navigate(typeof(Views.ImprovementView));
                break;
            case "issues":
                ContentFrame.Navigate(typeof(Views.IssuesView));
                break;
            default:
                ContentFrame.Navigate(typeof(Views.DashboardView));
                break;
        }
    }
    private async void ChangePassword_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var dialog = new ChangePasswordDialog();
        dialog.XamlRoot = this.Content.XamlRoot;
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var request = new ChangePasswordRequest(dialog.CurrentPassword, dialog.NewPassword);
            var success = await _authService.ChangePasswordAsync(request);
            
            var msgDialog = new ContentDialog
            {
                Title = success ? "Éxito" : "Error",
                Content = success ? "Contraseña actualizada correctamente." : "No se pudo actualizar la contraseña. Verifique su contraseña actual.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await msgDialog.ShowAsync();
        }
    }
}
