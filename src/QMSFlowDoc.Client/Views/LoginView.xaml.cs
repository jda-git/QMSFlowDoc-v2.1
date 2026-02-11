using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Client.Services;
using System;

namespace QMSFlowDoc.Client.Views;

public sealed partial class LoginView : Page
{
    private readonly IAuthService _authService;

    public LoginView()
    {
        this.InitializeComponent();
        // In a real app, use Dependency Injection. For MVP, instantiate directly.
        _authService = ((App)Application.Current).AuthService;
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await CheckBootstrapAsync();
    }

    private async System.Threading.Tasks.Task CheckBootstrapAsync()
    {
        try
        {
            var needsBootstrap = await _authService.NeedsBootstrapAsync();
            BootstrapButton.Visibility = needsBootstrap ? Visibility.Visible : Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = needsBootstrap ? Visibility.Visible : Visibility.Collapsed;
            BootstrapInfo.Visibility = needsBootstrap ? Visibility.Visible : Visibility.Collapsed;
            
            // In bootstrap mode, hide the login button to avoid confusion
            LoginButton.Visibility = needsBootstrap ? Visibility.Collapsed : Visibility.Visible;
        }
        catch { }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        LoginProgress.IsActive = true;
        LoginButton.IsEnabled = false;

        try
        {
            var success = await _authService.LoginAsync(UsernameBox.Text, PasswordBox.Password);
            if (success)
            {
                // Trigger event or navigate to main view
                ((App)Application.Current).NavigateToMain();
            }
            else
            {
                ErrorText.Text = "Error al iniciar sesión. Verifique sus credenciales.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginProgress.IsActive = false;
            LoginButton.IsEnabled = true;
        }
    }

    private async void BootstrapButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            ErrorText.Text = "Ingrese un usuario y contraseña para el administrador inicial.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (PasswordBox.Password.Length < 8)
        {
            ErrorText.Text = "La contraseña debe tener al menos 8 caracteres.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (PasswordBox.Password != ConfirmPasswordBox.Password)
        {
            ErrorText.Text = "Las contraseñas no coinciden.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        LoginProgress.IsActive = true;
        BootstrapButton.IsEnabled = false;

        try
        {
            var req = new QMSFlowDoc.Shared.DTOs.RegisterRequest(
                UsernameBox.Text,
                PasswordBox.Password,
                "System Administrator",
                "admin@qmsflowdoc.local",
                "Administrador"
            );

            var success = await _authService.BootstrapAsync(req);
            if (success)
            {
                ErrorText.Text = "Administrador creado. Ya puede iniciar sesión.";
                ErrorText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                ErrorText.Visibility = Visibility.Visible;
                BootstrapButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                ErrorText.Text = "Error al crear el administrador inicial.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginProgress.IsActive = false;
            BootstrapButton.IsEnabled = true;
        }
    }
    private void OnInputKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (LoginButton.Visibility == Visibility.Visible && LoginButton.IsEnabled)
            {
                LoginButton_Click(sender, e);
            }
        }
    }
}
