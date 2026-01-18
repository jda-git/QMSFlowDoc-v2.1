using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Views.Dialogs;

namespace QMSFlowDoc.Client.Views;

public sealed partial class StaffEditorView : Page
{
    private Guid? _staffId = null;
    private Guid? _userId = null;

    public StaffEditorView()
    {
        this.InitializeComponent();
        HiredDatePicker.Date = DateTimeOffset.Now;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            _staffId = id;
            TitleText.Text = "Expediente de Personal";
            SaveButton.Content = "Guardar Cambios";
            UsernameBox.IsEnabled = false; // Cannot change username for now
            await LoadStaffDetails(id);
        }
    }

    private async Task LoadStaffDetails(Guid id)
    {
        try
        {
            var app = (App)Application.Current;
            // Use GetStaffProfileByIdAsync to get full graph including Trainings
            var profile = await app.StaffService.GetStaffProfileByIdAsync(id);
            if (profile != null)
            {
                _userId = profile.UserId;
                FullNameBox.Text = profile.User?.FullName ?? "";
                EmailBox.Text = profile.User?.Email ?? "";
                UsernameBox.Text = profile.User?.Username ?? "";
                PositionBox.Text = profile.PositionTitle ?? "";
                DepartmentBox.Text = profile.Department ?? "";
                if (profile.HiredAt.HasValue) HiredDatePicker.Date = profile.HiredAt.Value;
                
                IsActiveCheck.IsChecked = profile.IsActive;

                // Set Role
                var roleName = profile.User?.Roles.FirstOrDefault()?.RoleName;
                foreach (ComboBoxItem item in RoleCombo.Items)
                {
                    if (string.Equals(item.Tag?.ToString(), roleName, StringComparison.OrdinalIgnoreCase))
                    {
                        RoleCombo.SelectedItem = item;
                        break;
                    }
                }
                
                // Show Reset Password button for Admins
                if (app.AuthService.IsAdmin)
                {
                    ResetPasswordButton.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error al cargar detalles: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }


    private async void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (!_userId.HasValue) return;

        var dialog = new ResetPasswordDialog { XamlRoot = this.XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var app = (App)Application.Current;
                var req = new ResetPasswordRequest(dialog.NewPassword);
                var success = await app.AuthService.ResetPasswordAsync(_userId.Value, req);
                
                var msg = new ContentDialog
                {
                    Title = success ? "Éxito" : "Error",
                    Content = success ? "Contraseña restablecida correctamente." : "Error al restablecer la contraseña.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await msg.ShowAsync();
            }
            catch(Exception ex)
            {
                ErrorText.Text = $"Error: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveButton.IsEnabled = false;
            ErrorText.Visibility = Visibility.Collapsed;

            var app = (App)Application.Current;
            var fullName = FullNameBox.Text.Trim();
            var email = EmailBox.Text.Trim();
            var username = UsernameBox.Text.Trim();
            var position = PositionBox.Text.Trim();
            var department = DepartmentBox.Text.Trim();
            var hired = HiredDatePicker.Date.DateTime;
            var roleItem = RoleCombo.SelectedItem as ComboBoxItem;
            var roleName = roleItem?.Tag?.ToString() ?? "Staff";
            var isActive = IsActiveCheck.IsChecked ?? true;

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username))
            {
                ErrorText.Text = "Nombre completo y usuario son obligatorios.";
                ErrorText.Visibility = Visibility.Visible;
                SaveButton.IsEnabled = true;
                return;
            }

            if (_staffId.HasValue)
            {
                // Update
                var updateReq = new UpdateStaffProfileRequest(
                    _staffId.Value,
                    fullName,
                    email,
                    position,
                    department,
                    hired,
                    roleName,
                    isActive
                );

                var result = await app.StaffService.UpdateStaffProfileAsync(updateReq);
                if (result != null)
                {
                    Frame.GoBack();
                }
                else
                {
                    ErrorText.Text = "Error al actualizar perfil.";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // Create
                var password = PasswordBox.Password;
                if (string.IsNullOrWhiteSpace(password))
                {
                    ErrorText.Text = "La contraseña es obligatoria para nuevos usuarios.";
                    ErrorText.Visibility = Visibility.Visible;
                    SaveButton.IsEnabled = true;
                    return;
                }

                // 1. Create User
                var regReq = new RegisterRequest(
                    username,
                    password,
                    fullName,
                    email,
                    roleName
                );

                var userId = await app.AuthService.RegisterAsync(regReq);
                
                if (userId != null)
                {
                    // 2. Create Profile
                    var profileReq = new CreateStaffProfileRequest(
                        userId.Value,
                        position,
                        department,
                        hired
                    );

                    var profile = await app.StaffService.CreateStaffProfileAsync(profileReq);
                    if (profile != null)
                    {
                        Frame.GoBack();
                    }
                    else
                    {
                        ErrorText.Text = "Usuario creado, pero falló la creación del perfil.";
                        ErrorText.Visibility = Visibility.Visible;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            // Initial navigation fallback if any
        }
    }
}
