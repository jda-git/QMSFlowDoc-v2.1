using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class CompetencyAuthView : Page
{
    public CompetencyAuthView()
    {
        this.InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            LoadingBar.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            AuthList.Visibility = Visibility.Collapsed;

            var app = (App)Application.Current;
            var staffName = string.IsNullOrWhiteSpace(StaffFilterBox.Text) ? null : StaffFilterBox.Text;
            
            var statusItem = StatusFilterBox.SelectedItem as ComboBoxItem;
            var status = statusItem?.Tag?.ToString(); // "Active", "Expired", or ""

            var auths = await app.AuthorizationService.GetAllAuthorizationsAsync(staffName, status);

            AuthList.ItemsSource = auths;
            
            if (!auths.Any())
            {
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                AuthList.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"No se pudieron cargar las autorizaciones: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await LoadData();
    }

    private async void GrantAuthorization_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var dialog = new QMSFlowDoc.Client.Views.Dialogs.GrantAuthorizationDialog();
        dialog.XamlRoot = this.XamlRoot;

        var staffList = await app.StaffService.GetStaffAsync();
        dialog.EnableStaffSelection(staffList);

        var catalog = await app.CompetencyService.GetCatalogAsync();
        var compList = catalog.Select(c => new CompetencyDto 
        { 
            Id = c.Id, 
            Code = c.Code, 
            Name = c.Name, 
            Description = c.Description,
            Category = c.Category,
            RequiredFrequencyMonths = c.RequiredFrequencyMonths
        });
        dialog.LoadCompetencies(compList);

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
             if (string.IsNullOrEmpty(dialog.SelectedStaffId)) return;

             var req = new GrantAuthorizationRequest(
                 Guid.Parse(dialog.SelectedStaffId),
                 dialog.TaskName,
                 dialog.Description,
                 dialog.ValidFrom,
                 dialog.ValidUntil,
                 GrantedByUserId: app.AuthService.CurrentUserId,
                 CompetencyId: dialog.SelectedCompetencyId
             );
             
             bool success = await app.AuthorizationService.GrantAuthorizationAsync(req);
             if (success)
             {
                 await LoadData();
             }
             else
             {
                  var errDialog = new ContentDialog
                  {
                      Title = "Error",
                      Content = "No se pudo otorgar la autorización.",
                      CloseButtonText = "OK",
                      XamlRoot = this.XamlRoot
                  };
                  await errDialog.ShowAsync();
             }
        }
    }

    private async void DeleteAuth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GlobalAuthorizationDto dto)
        {
             var dialog = new ContentDialog
            {
                Title = "Confirmar Revocación/Eliminación",
                Content = $"¿Está seguro de que desea eliminar la autorización '{dto.AuthorizationName}' de {dto.StaffName}? Esta acción es irreversible.",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
             if (result == ContentDialogResult.Primary)
            {
                 if (dto.Id == Guid.Empty)
                 {
                      var warnDialog = new ContentDialog
                      {
                          Title = "Error",
                          Content = "ID de autorización inválido.",
                          CloseButtonText = "OK",
                          XamlRoot = this.XamlRoot
                      };
                      await warnDialog.ShowAsync();
                      return;
                 }

                 var app = (App)Application.Current;
                 bool success = await app.AuthorizationService.DeleteAuthorizationAsync(dto.Id);
                 if (success)
                 {
                     var successDialog = new ContentDialog
                      {
                          Title = "Éxito",
                          Content = "Autorización eliminada correctamente.",
                          CloseButtonText = "OK",
                          XamlRoot = this.XamlRoot
                      };
                      await successDialog.ShowAsync();
                     await LoadData();
                 }
                 else
                 {
                      var errDialog = new ContentDialog
                      {
                          Title = "Error",
                          Content = "No se pudo eliminar la autorización.",
                          CloseButtonText = "OK",
                          XamlRoot = this.XamlRoot
                      };
                      await errDialog.ShowAsync();
                 }
            }
        }
    }
}
