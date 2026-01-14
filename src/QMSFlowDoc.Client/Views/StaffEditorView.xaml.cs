using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

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
            
            // Show all ISO tabs in edit mode
            TrainingTab.Visibility = Visibility.Visible;
            CompetencyTab.Visibility = Visibility.Visible;
            AuthorizationTab.Visibility = Visibility.Visible;
        }
        else
        {
            // Hide ISO tabs in create mode
            TrainingTab.Visibility = Visibility.Collapsed;
            CompetencyTab.Visibility = Visibility.Collapsed;
            AuthorizationTab.Visibility = Visibility.Collapsed;
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
                    if (item.Tag?.ToString() == roleName)
                    {
                        RoleCombo.SelectedItem = item;
                        break;
                    }
                }
                
                // Populate Training List
                TrainingList.ItemsSource = profile.Trainings.Select(t => new StaffTrainingDto(
                    t.Id,
                    t.TrainingActivityId,
                    t.TrainingActivity?.Title ?? "Desconocido",
                    t.TrainingActivity?.Provider ?? "",
                    t.CompletedAt ?? DateTime.MinValue,
                    t.TrainingActivity?.Hours ?? 0m,
                    t.Result ?? ""
                )).ToList();
                
                // Populate Competency List
                CompetencyList.ItemsSource = await app.CompetencyService.GetStaffEvaluationsAsync(id);
                
                // Populate Authorization List
                AuthorizationList.ItemsSource = await app.AuthorizationService.GetStaffAuthorizationsAsync(id);
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error al cargar detalles: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async void AddTraining_Click(object sender, RoutedEventArgs e)
    {
        if (!_staffId.HasValue)
        {
            ErrorText.Text = "Debe guardar el perfil antes de registrar formación.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var dialog = new Views.Dialogs.AddTrainingDialog
        {
            XamlRoot = this.XamlRoot
        };
        
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var req = new RegisterTrainingRequest(
                    _staffId.Value,
                    dialog.TrainingTitle,
                    dialog.Provider,
                    dialog.Hours,
                    dialog.CompletedAt,
                    dialog.Result,
                    dialog.Notes
                );

                var app = (App)Application.Current;
                var success = await app.StaffService.RegisterTrainingAsync(req);
                
                if (success)
                {
                    // Refresh training list
                    await LoadStaffDetails(_staffId.Value);
                    ErrorText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ErrorText.Text = "Error al registrar la formación.";
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

    private async void AssessCompetency_Click(object sender, RoutedEventArgs e)
    {
        if (!_staffId.HasValue)
        {
            ErrorText.Text = "Debe guardar el perfil antes de evaluar competencias.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var dialog = new Views.Dialogs.AssessCompetencyDialog
        {
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var req = new AssessCompetencyRequest(
                    _staffId.Value,
                    dialog.CompetencyName,
                    dialog.Area,
                    dialog.Outcome,
                    dialog.EvaluationDate,
                    dialog.ValidUntil,
                    dialog.Evidence
                );

                var app = (App)Application.Current;
                var result = await app.StaffService.AssessCompetencyAsync(req);
                
                if (result != null)
                {
                    // Refresh
                    CompetencyList.ItemsSource = await app.CompetencyService.GetStaffEvaluationsAsync(_staffId.Value);
                    ErrorText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Error: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }

    private async void GrantAuthorization_Click(object sender, RoutedEventArgs e)
    {
        if (!_staffId.HasValue)
        {
            ErrorText.Text = "Debe guardar el perfil antes de emitir autorizaciones.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var dialog = new Views.Dialogs.GrantAuthorizationDialog
        {
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var req = new GrantAuthorizationRequest(
                    _staffId.Value,
                    dialog.TaskName,
                    dialog.Description,
                    dialog.ValidFrom,
                    dialog.ValidUntil
                );

                var app = (App)Application.Current;
                var success = await app.AuthorizationService.GrantAuthorizationAsync(req);
                
                if (success)
                {
                    // Refresh
                    AuthorizationList.ItemsSource = await app.AuthorizationService.GetStaffAuthorizationsAsync(_staffId.Value);
                    ErrorText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Error: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(FullNameBox.Text) || string.IsNullOrWhiteSpace(UsernameBox.Text))
        {
            ErrorText.Text = "Nombre y Usuario son obligatorios.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (!_staffId.HasValue && string.IsNullOrWhiteSpace(PasswordBox.Password))
        {
            ErrorText.Text = "La contraseña es obligatoria para nuevas fichas.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var app = (App)Application.Current;
            var roleName = (RoleCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Consultor";

            if (_staffId.HasValue)
            {
                // UPDATE
                var updateRequest = new UpdateStaffProfileRequest(
                    _staffId.Value,
                    FullNameBox.Text,
                    EmailBox.Text,
                    PositionBox.Text,
                    DepartmentBox.Text,
                    HiredDatePicker.Date.DateTime,
                    roleName,
                    IsActiveCheck.IsChecked ?? true
                );

                var result = await app.StaffService.UpdateStaffProfileAsync(updateRequest);
                if (result != null)
                {
                    Frame.GoBack();
                }
            }
            else
            {
                // CREATE NEW
                // 1. Register User
                var regRequest = new RegisterRequest(
                    UsernameBox.Text,
                    PasswordBox.Password,
                    FullNameBox.Text,
                    EmailBox.Text,
                    roleName
                );

                try 
                {
                    var userId = await app.AuthService.RegisterAsync(regRequest);
                    if (userId == null)
                    {
                        ErrorText.Text = "Error desconocido al registrar usuario.";
                        ErrorText.Visibility = Visibility.Visible;
                        return;
                    }

                    // 2. Create Staff Profile
                    var profileRequest = new CreateStaffProfileRequest(
                        userId.Value,
                        PositionBox.Text,
                        DepartmentBox.Text,
                        HiredDatePicker.Date.DateTime
                    );

                    var profile = await app.StaffService.CreateStaffProfileAsync(profileRequest);
                    if (profile != null)
                    {
                        Frame.GoBack();
                    }
                }
                catch (Exception authEx)
                {
                    // This will now catch the specific "username exists" or other server errors
                    ErrorText.Text = authEx.Message;
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

    private async void EditTraining_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid trainingId)
        {
            var dialog = new ContentDialog
            {
                Title = "Editar/Eliminar Formación",
                Content = "¿Qué desea hacer con este registro de formación?",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var app = (App)Application.Current;
                    var success = await app.StaffService.DeleteTrainingAsync(trainingId);
                    if (success && _staffId.HasValue)
                    {
                        await LoadStaffDetails(_staffId.Value);
                    }
                }
                catch (Exception ex)
                {
                    ErrorText.Text = $"Error al eliminar: {ex.Message}";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
        }
    }

    private async void EditCompetency_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid competencyEvalId)
        {
            var dialog = new ContentDialog
            {
                Title = "Editar/Eliminar Competencia",
                Content = "¿Qué desea hacer con esta evaluación de competencia?",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var app = (App)Application.Current;
                    var success = await app.CompetencyService.DeleteEvaluationAsync(competencyEvalId);
                    if (success && _staffId.HasValue)
                    {
                        CompetencyList.ItemsSource = await app.CompetencyService.GetStaffEvaluationsAsync(_staffId.Value);
                    }
                }
                catch (Exception ex)
                {
                    ErrorText.Text = $"Error al eliminar: {ex.Message}";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
        }
    }

    private async void EditAuthorization_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid authId)
        {
            var dialog = new ContentDialog
            {
                Title = "Editar/Eliminar Autorización",
                Content = "¿Qué desea hacer con esta autorización?",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var app = (App)Application.Current;
                    var success = await app.AuthorizationService.DeleteAuthorizationAsync(authId);
                    if (success && _staffId.HasValue)
                    {
                        AuthorizationList.ItemsSource = await app.AuthorizationService.GetStaffAuthorizationsAsync(_staffId.Value);
                    }
                }
                catch (Exception ex)
                {
                    ErrorText.Text = $"Error al eliminar: {ex.Message}";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
