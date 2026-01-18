using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Client.Views.Dialogs;

namespace QMSFlowDoc.Client.Views;

public sealed partial class CompetencyMatrixView : Page
{
    private readonly ICompetencyService _competencyService;
    private readonly IStaffService _staffService;

    public CompetencyMatrixView()
    {
        this.InitializeComponent();
        var app = (App)Application.Current;
        _competencyService = app.CompetencyService;
        _staffService = app.StaffService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await BuildMatrixAsync();
    }

    private async void ReloadMatrix_Click(object sender, RoutedEventArgs e)
    {
        await BuildMatrixAsync();
    }

    private async Task BuildMatrixAsync()
    {
        MatrixGrid.Children.Clear();
        MatrixGrid.RowDefinitions.Clear();
        MatrixGrid.ColumnDefinitions.Clear();

        var staffList = (await _staffService.GetStaffAsync()).OrderBy(s => s.FullName).ToList();
        var compList = (await _competencyService.GetCatalogAsync()).OrderBy(c => c.Category).ThenBy(c => c.Name).ToList();

        if (!staffList.Any() || !compList.Any()) 
        {
            MatrixGrid.Children.Add(new TextBlock { Text = "No hay datos suficientes (Personal o Competencias) para generar la matriz.", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            return;
        }

        // Define Rows and Cols
        // Row 0: Headers
        // Col 0: Staff Names
        
        MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header Row
        MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Staff Name Col

        // Add Competency Columns
        foreach (var comp in compList)
        {
            MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            
            // Header
            var header = new TextBlock 
            { 
                Text = comp.Name, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(4), 
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 11
            };
            ToolTipService.SetToolTip(header, comp.Description);
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, MatrixGrid.ColumnDefinitions.Count - 1);
            MatrixGrid.Children.Add(header);
        }

        // Add Rows for Staff
        int rowIndex = 1;
        foreach (var staff in staffList)
        {
             MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

             // Staff Name Header
             var nameBlock = new TextBlock { Text = staff.FullName, FontWeight = FontWeights.SemiBold, Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center };
             Grid.SetRow(nameBlock, rowIndex);
             Grid.SetColumn(nameBlock, 0);
             MatrixGrid.Children.Add(nameBlock);
             
             // Get Evaluations
             var evaluations = await _competencyService.GetStaffEvaluationsAsync(staff.Id);

             // Fill Cells
             int colIndex = 1;
             foreach (var comp in compList)
             {
                 var eval = evaluations.FirstOrDefault(e => e.CompetencyId == comp.Id || e.CompetencyName == comp.Name); // Fallback to name match for legacy
                 
                 var cell = new Border 
                 { 
                     Margin = new Thickness(2), 
                     CornerRadius = new CornerRadius(4),
                     Height = 30
                 };
                 
                 // Make cell interactive
                 cell.Tapped += async (s, args) => 
                 {
                      var dialog = new EvaluateCompetencyDialog();
                      dialog.XamlRoot = this.XamlRoot;
                      var staffDto = new StaffListDto { Id = staff.Id, FullName = staff.FullName };
                      var compDto = new CompetencyDto { Id = comp.Id, Name = comp.Name };
                      
                      dialog.LoadStaff(new List<StaffListDto> { staff });
                      dialog.LoadCompetencies(new List<CompetencyDto> { compDto });
                      
                      if (eval != null) dialog.LoadData(eval);
                      else dialog.SetFixedContext(staff.Id, comp.Id);
                      
                      if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                      {
                           var dto = new CompetencyEvaluationDto
                           {
                               Id = eval?.Id ?? Guid.NewGuid(),
                               StaffId = staff.Id,
                               CompetencyId = comp.Id,
                               CompetencyName = comp.Name,
                               EvaluationDate = dialog.EvaluationDate,
                               ValidUntil = dialog.ValidUntil,
                               Outcome = dialog.Outcome,
                               Evidence = dialog.Evidence,
                               EvaluatorName = dialog.Evaluator,
                               Area = dialog.Area ?? eval?.Area
                           };
                           try
                           {
                               await _competencyService.UpsertEvaluationAsync(dto);
                               await BuildMatrixAsync();
                           }
                           catch (Exception ex)
                           {
                                var errDialog = new ContentDialog
                                {
                                    Title = "Error",
                                    Content = ex.Message, 
                                    CloseButtonText = "OK",
                                    XamlRoot = this.XamlRoot
                                };
                                await errDialog.ShowAsync();
                           }
                      }
                 };

                 var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, FontSize=10, Foreground=new SolidColorBrush(Colors.White) };
                 
                 if (eval != null)
                 {
                     bool isExpired = eval.ValidUntil.HasValue && eval.ValidUntil.Value < DateTime.Now;
                     if (isExpired)
                     {
                         cell.Background = new SolidColorBrush(Colors.OrangeRed);
                         text.Text = "Caducado";
                     }
                     else
                     {
                         cell.Background = new SolidColorBrush(Colors.Green);
                         text.Text = "Vigente";
                     }
                     ToolTipService.SetToolTip(cell, $"Evaluado: {eval.EvaluationDate:d}\nVence: {(eval.ValidUntil.HasValue ? eval.ValidUntil.Value.ToString("d") : "N/A")}\nclick para editar");
                 }
                 else
                 {
                     cell.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(50, 200, 200, 200)); // Light Gray
                     text.Text = "+"; // Show + to indicate action
                     text.Foreground = new SolidColorBrush(Colors.Gray);
                     ToolTipService.SetToolTip(cell, "Click para evaluar");
                 }
                 
                 cell.Child = text;
                 Grid.SetRow(cell, rowIndex);
                 Grid.SetColumn(cell, colIndex);
                 MatrixGrid.Children.Add(cell);

                 colIndex++;
             }

             rowIndex++;
        }
    }

    private void ExportMatrix_Click(object sender, RoutedEventArgs e)
    {
         // Placeholder for CSV export
    }
}
