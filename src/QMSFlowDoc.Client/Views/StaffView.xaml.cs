using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class StaffView : Page
{
    private readonly IStaffService _staffService;
    public ObservableCollection<StaffListDto> StaffMembers { get; } = new();

    public StaffView()
    {
        this.InitializeComponent();
        _staffService = ((App)Application.Current).StaffService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadStaff();
    }

    private async Task LoadStaff()
    {
        try
        {
            var list = await _staffService.GetStaffAsync();
            StaffMembers.Clear();
            foreach (var item in list)
            {
                StaffMembers.Add(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading staff: {ex.Message}");
        }
    }

    private void AddStaff_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(StaffEditorView));
    }


    private async void PurgeUsers_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "PELIGRO: Limpieza de Registros",
            Content = "¿Está seguro de que desea borrar TODOS los usuarios y fichas de personal (excepto admin)?\nEsta acción es irreversible y soluciona problemas de duplicados invisibles.",
            PrimaryButtonText = "Borrar Todo",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var app = (App)Application.Current;
                await app.AuthService.PurgeUsersAsync();
                await LoadStaff();
                
                var okDialog = new ContentDialog
                {
                    Title = "Limpieza Completada",
                    Content = "Se han eliminado los registros correctamente.",
                    CloseButtonText = "Ok",
                    XamlRoot = this.XamlRoot
                };
                await okDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"No se pudo completar la limpieza: {ex.Message}",
                    CloseButtonText = "Ok",
                    XamlRoot = this.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }
    }

    private void EditStaff_Click(object sender, RoutedEventArgs e)
    {
        if (StaffList.SelectedItem is StaffListDto selected)
        {
            Frame.Navigate(typeof(StaffEditorView), selected.Id);
        }
    }

    private async void DeleteStaff_Click(object sender, RoutedEventArgs e)
    {
        if (StaffList.SelectedItem is StaffListDto selected)
        {
            var dialog = new ContentDialog
            {
                Title = "Confirmar eliminación",
                Content = $"¿Está seguro de que desea eliminar la ficha de {selected.FullName}?\nEsta acción también eliminará su usuario de acceso.",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    await _staffService.DeleteStaffProfileAsync(selected.Id);
                    await LoadStaff();
                }
                catch (Exception ex)
                {
                    var errDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"No se pudo eliminar: {ex.Message}",
                        CloseButtonText = "Ok",
                        XamlRoot = this.XamlRoot
                    };
                    await errDialog.ShowAsync();
                }
            }
        }
    }
}
