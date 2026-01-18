using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class CompetencyTrainingView : Page
{
    public CompetencyTrainingView()
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
            LoadingState.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            TrainingList.Visibility = Visibility.Collapsed;

            var app = (App)Application.Current;
            var staffName = string.IsNullOrWhiteSpace(StaffFilterBox.Text) ? null : StaffFilterBox.Text;
            var trainingName = string.IsNullOrWhiteSpace(TrainingFilterBox.Text) ? null : TrainingFilterBox.Text;
            
            // Fix DatePicker nullability logic
            DateTime? from = FromFilterPicker.Date?.DateTime;
            DateTime? to = ToFilterPicker.Date?.DateTime;

            var trainings = await app.StaffService.GetAllTrainingsAsync(staffName, trainingName, from, to);

            TrainingList.ItemsSource = trainings;
            
            if (!trainings.Any())
            {
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                TrainingList.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = $"No se pudieron cargar los datos de formación: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            LoadingState.Visibility = Visibility.Collapsed;
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await LoadData();
    }

    private async void AddTraining_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        var dialog = new QMSFlowDoc.Client.Views.Dialogs.AddTrainingDialog();
        dialog.XamlRoot = this.XamlRoot;

        // Load staff list for selection
        var staffList = await app.StaffService.GetStaffAsync();
        dialog.EnableStaffSelection(staffList);

        var catalog = await app.CompetencyService.GetCatalogAsync();
        var compList = catalog.Select(c => new CompetencyDto 
        { 
            Id = c.Id, 
            Code = c.Code, 
            Name = c.Name, 
            Description = c.Description,
            Category = c.Category
        });
        dialog.LoadCompetencies(compList);

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrEmpty(dialog.SelectedStaffId)) return;

            var req = new RegisterTrainingRequest(
                Guid.Parse(dialog.SelectedStaffId),
                dialog.TrainingTitle,
                dialog.Provider,
                dialog.Hours,
                dialog.CompletedAt,
                dialog.Result,
                dialog.Notes,
                dialog.SelectedCompetencyId
            );

            bool success = await app.StaffService.RegisterTrainingAsync(req);
            if (success)
            {
                await LoadData();
            }
            else
            {
                var errDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "No se pudo registrar la formación.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }
    }

    private async void EditTraining_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GlobalTrainingDto dto)
        {
             var app = (App)Application.Current;
             var dialog = new QMSFlowDoc.Client.Views.Dialogs.AddTrainingDialog();
             dialog.XamlRoot = this.XamlRoot;
             
             // Load Competencies
            var catalog = await app.CompetencyService.GetCatalogAsync();
            var compList = catalog.Select(c => new CompetencyDto 
            { 
                Id = c.Id, 
                Code = c.Code, 
                Name = c.Name, 
                Description = c.Description,
                Category = c.Category
            });
            dialog.LoadCompetencies(compList);

             // Map Global to StaffTrainingDto for the dialog
             var staffDto = new StaffTrainingDto(
                 dto.Id,
                 dto.TrainingActivityId,
                 dto.Title,
                 dto.Provider,
                 dto.CompletionDate,
                 dto.Hours,
                 dto.Result,
                 dto.CompetencyId
             );
             
             dialog.LoadData(staffDto);
             
             var result = await dialog.ShowAsync();
             if (result == ContentDialogResult.Primary)
             {
                  var req = new UpdateTrainingRequest(
                      dto.Id,
                      dto.StaffId,
                      dialog.TrainingTitle,
                      dialog.Provider,
                      dialog.Hours,
                      dialog.CompletedAt,
                      dialog.Result,
                      dialog.Notes,
                      dialog.SelectedCompetencyId
                  );
                  
                  bool success = await app.StaffService.UpdateTrainingAsync(req);
                  if (success)
                  {
                      await LoadData();
                  }
                  else
                  {
                       var errDialog = new ContentDialog
                       {
                           Title = "Error",
                           Content = "No se pudo actualizar la formación.",
                           CloseButtonText = "OK",
                           XamlRoot = this.XamlRoot
                       };
                       await errDialog.ShowAsync();
                  }
             }

             else if (result == ContentDialogResult.Secondary)
             {
                 // Delete requested from dialog (if enabled)
                 // But my XAML has a separate Delete button.
             }
        }
    }

    private async void DeleteTraining_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GlobalTrainingDto dto)
        {
             var dialog = new ContentDialog
            {
                Title = "Confirmar Eliminación",
                Content = $"¿Está seguro de que desea eliminar la formación '{dto.Title}' de {dto.StaffName}?",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                 var app = (App)Application.Current;
                 bool success = await app.StaffService.DeleteTrainingAsync(dto.Id);
                 if (success)
                 {
                     await LoadData();
                 }
                 else
                 {
                      // Error msg
                 }
            }
        }
    }
}
