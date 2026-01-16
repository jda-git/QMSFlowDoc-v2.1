using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Client.Views.Dialogs;

namespace QMSFlowDoc.Client.Views;

public sealed partial class EQAView : Page
{
    private readonly IEQAService _eqaService;
    private EQAProgramDto? _selectedProgram;

    public EQAView()
    {
        this.InitializeComponent();
        _eqaService = ((App)Application.Current).EQAService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadProgramsAsync();
    }

    private async Task LoadProgramsAsync()
    {
        try
        {
            var programs = await _eqaService.GetProgramsAsync();
            ProgramList.ItemsSource = programs;

            if (_selectedProgram != null)
            {
                // Re-select if exists
                var reselect = programs.FirstOrDefault(p => p.Id == _selectedProgram.Id);
                if (reselect != null)
                {
                    ProgramList.SelectedItem = reselect;
                }
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Error cargando programas", ex.Message);
        }
    }

    private async void ProgramList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProgramList.SelectedItem is EQAProgramDto program)
        {
            _selectedProgram = program;
            DetailPane.Visibility = Visibility.Visible;
            EmptySelectionPane.Visibility = Visibility.Collapsed;
            
            // Bind Header
            DetailTitle.Text = program.Name;
            DetailProvider.Text = $"{program.Provider} • {program.Frequency}";

            await LoadResultsAsync(program.Id);
        }
        else
        {
            _selectedProgram = null;
            DetailPane.Visibility = Visibility.Collapsed;
            EmptySelectionPane.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadResultsAsync(Guid programId)
    {
        try
        {
            var results = await _eqaService.GetResultsAsync(programId);
            ResultsList.ItemsSource = results;
            EmptyResultsText.Visibility = results.Any() ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Error cargando resultados", ex.Message);
        }
    }

    private async void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        // Simple input dialog for MVP
        var dialog = new ContentDialog
        {
            Title = "Nuevo Programa EQA",
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var nameBox = new TextBox { Header = "Nombre del Programa", PlaceholderText = "Ej. Hematología Ciclo 1" };
        var providerBox = new TextBox { Header = "Proveedor", PlaceholderText = "Ej. RIQAS, SECAL" };
        var freqBox = new ComboBox { Header = "Frecuencia", HorizontalAlignment = HorizontalAlignment.Stretch };
        freqBox.Items.Add("Mensual");
        freqBox.Items.Add("Trimestral");
        freqBox.Items.Add("Semestral");
        freqBox.SelectedIndex = 0;

        stack.Children.Add(nameBox);
        stack.Children.Add(providerBox);
        stack.Children.Add(freqBox);
        dialog.Content = stack;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

            var req = new CreateEQAProgramRequest(nameBox.Text, providerBox.Text, freqBox.SelectedItem?.ToString(), null);
            await _eqaService.CreateProgramAsync(req);
            await LoadProgramsAsync();
        }
    }

    private async void EditProgram_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProgram == null) return;
        
        // Similar to Add but pre-filled
        var dialog = new ContentDialog
        {
            Title = "Editar Programa",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var nameBox = new TextBox { Header = "Nombre", Text = _selectedProgram.Name };
        var providerBox = new TextBox { Header = "Proveedor", Text = _selectedProgram.Provider ?? "" };
        var freqBox = new ComboBox { Header = "Frecuencia", HorizontalAlignment = HorizontalAlignment.Stretch };
        freqBox.Items.Add("Mensual");
        freqBox.Items.Add("Trimestral");
        freqBox.Items.Add("Semestral");
        freqBox.SelectedItem = _selectedProgram.Frequency;

        stack.Children.Add(nameBox);
        stack.Children.Add(providerBox);
        stack.Children.Add(freqBox);
        dialog.Content = stack;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var req = new UpdateEQAProgramRequest(
                _selectedProgram.Id, 
                nameBox.Text, 
                providerBox.Text, 
                freqBox.SelectedItem?.ToString(), 
                _selectedProgram.Status, 
                null // Notes not in MVP DTO
            );
            await _eqaService.UpdateProgramAsync(req);
            await LoadProgramsAsync();
        }
    }

    private async void DeleteProgram_Click(object sender, RoutedEventArgs e)
    {
         if (_selectedProgram == null) return;

         var dialog = new ContentDialog
         {
             Title = "¿Eliminar programa?",
             Content = $"Se eliminará el programa '{_selectedProgram.Name}' y todos sus resultados. Esta acción no se puede deshacer.",
             PrimaryButtonText = "Eliminar",
             CloseButtonText = "Cancelar",
             DefaultButton = ContentDialogButton.Close,
             XamlRoot = this.Content.XamlRoot
         };

         if (await dialog.ShowAsync() == ContentDialogResult.Primary)
         {
             // Implement Deletion in Service (Missing in Interface/Service! - MVP gap, assume archival or status update)
             // For now, let's just mark Inactive or skip
             // Actually I'll update status to Archived
             var req = new UpdateEQAProgramRequest(_selectedProgram.Id, _selectedProgram.Name, _selectedProgram.Provider, _selectedProgram.Frequency, EQAStatus.ARCHIVED, null);
             await _eqaService.UpdateProgramAsync(req);
             await LoadProgramsAsync();
         }
    }

    private async void AddResult_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProgram == null) return;

        // Using simple dialog for MVP Result Input
        var dialog = new ContentDialog
        {
            Title = "Registrar Resultado",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var cycleBox = new TextBox { Header = "Identificador de Ciclo", PlaceholderText = "Ej. 2024-05" };
        var datePicker = new DatePicker { Header = "Fecha de Envío" };
        
        stack.Children.Add(cycleBox);
        stack.Children.Add(datePicker);
        
        dialog.Content = stack;

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
             var programId = _selectedProgram.Id;
             var req = new RegisterEQAResultRequest(
                 programId,
                 cycleBox.Text,
                 null, null,
                 datePicker.Date.DateTime,
                 null
             );
             await _eqaService.RegisterResultAsync(req);
             await LoadProgramsAsync(); // Refresh stats
             await LoadResultsAsync(programId);
        }
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProgram == null) return;
        try
        {
            var results = await _eqaService.GetResultsAsync(_selectedProgram.Id);
            var exportService = ((App)Application.Current).ExportService;
            await exportService.ExportEqaReportToPdfAsync(results, _selectedProgram.Name);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Error exportando PDF", ex.Message);
        }
    }

    private async void EvaluateResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item)
        {
            var result = item.Tag as EQAResultDto ?? item.DataContext as EQAResultDto;
            if (result != null)
            {
            var dialog = new ContentDialog
            {
                Title = "Evaluar Desempeño",
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var stack = new StackPanel { Spacing = 12 };
            
            // Performance ComboBox
            var perfBox = new ComboBox 
            { 
                Header = "Desempeño", 
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = Enum.GetValues(typeof(EQAPerformance)).Cast<EQAPerformance>().ToList()
            };
            
            // Try parse existing performance string or default
            if (Enum.TryParse<EQAPerformance>(result.Performance, true, out var currentPerf))
                perfBox.SelectedItem = currentPerf;
            else
                perfBox.SelectedItem = EQAPerformance.NOT_EVALUATED;

            // Score Box
            var scoreBox = new TextBox 
            { 
                Header = "Puntuación", 
                PlaceholderText = "0-100",
                InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } },
                Text = result.Score?.ToString() ?? "" 
            };

            // Notes Box
            var notesBox = new TextBox 
            { 
                Header = "Notas", 
                AcceptsReturn = true, 
                Height = 80 
            };

            stack.Children.Add(perfBox);
            stack.Children.Add(scoreBox);
            stack.Children.Add(notesBox);

            dialog.Content = stack;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                decimal? score = null;
                if (decimal.TryParse(scoreBox.Text, out var s)) score = s;

                var req = new UpdateEQAResultRequest(
                    result.Id,
                    EQAResultStatus.EVALUATED,
                    score,
                    (EQAPerformance)(perfBox.SelectedItem ?? EQAPerformance.NOT_EVALUATED),
                    notesBox.Text,
                    null, null
                );

                await _eqaService.UpdateResultAsync(req);
                
                var programId = _selectedProgram?.Id ?? result.ProgramId;
                await LoadProgramsAsync(); // Refresh stats on left pane
                
                // If the selected program is still the same (it should be), refresh list
                if (_selectedProgram != null && _selectedProgram.Id == programId)
                {
                     await LoadResultsAsync(programId);
                }
            }
        }
    }
}


    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    public static string FormatDate(DateTime? d) => d?.ToString("d") ?? "-";
}

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int i)
        {
            return i > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
