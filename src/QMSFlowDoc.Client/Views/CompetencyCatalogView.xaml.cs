using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.UI.Text; // For FontWeights

namespace QMSFlowDoc.Client.Views;

public sealed partial class CompetencyCatalogView : Page
{
    private readonly ICompetencyService _competencyService;
    private Competency? _selectedCompetency;

    public CompetencyCatalogView()
    {
        this.InitializeComponent();
        
        // Resolve services safely (fallback if not registered yet, though App should have them)
        var app = (App)Application.Current;
        _competencyService = app.CompetencyService; 
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadCompetenciesAsync();
    }

    private async Task LoadCompetenciesAsync()
    {
        var competencies = await _competencyService.GetCatalogAsync();
        CompetencyList.ItemsSource = competencies.OrderBy(c => c.Code);
        
        if (_selectedCompetency != null)
        {
             // Reselect
             var reselect = competencies.FirstOrDefault(c => c.Id == _selectedCompetency.Id);
             if (reselect != null) CompetencyList.SelectedItem = reselect;
        }
    }

    private void CompetencyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CompetencyList.SelectedItem is Competency comp)
        {
            _selectedCompetency = comp;
            DetailPane.Visibility = Visibility.Visible;
            EmptySelectionPane.Visibility = Visibility.Collapsed;

            DetailCode.Text = comp.Code;
            DetailName.Text = comp.Name;
            DetailDescription.Text = comp.Description ?? "Sin descripción";
            DetailCategory.Text = comp.Category ?? "General";
            DetailFrequency.Text = comp.RequiredFrequencyMonths.HasValue 
                ? $"Frecuencia: {comp.RequiredFrequencyMonths} meses" 
                : "Sin frecuencia establecida";
        }
        else
        {
            _selectedCompetency = null;
            DetailPane.Visibility = Visibility.Collapsed;
            EmptySelectionPane.Visibility = Visibility.Visible;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Filter list implementation (omitted for brevity, can be added if needed)
        // Simple client-side filtering logic here
    }

    private async void RefreshList_Click(object sender, RoutedEventArgs e)
    {
        await LoadCompetenciesAsync();
    }

    private async void AddCompetency_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Nueva Competencia",
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var codeBox = new TextBox { Header = "Código", PlaceholderText = "COMP-001" };
        var nameBox = new TextBox { Header = "Nombre" };
        var catBox = new ComboBox { Header = "Categoría", HorizontalAlignment = HorizontalAlignment.Stretch };
        catBox.Items.Add("General");
        catBox.Items.Add("Técnica");
        catBox.Items.Add("Seguridad");
        catBox.SelectedIndex = 0;
        var freqBox = new NumberBox { Header = "Frecuencia (Meses)", Value = 12, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var descBox = new TextBox { Header = "Descripción", AcceptsReturn = true, Height = 60 };

        stack.Children.Add(codeBox);
        stack.Children.Add(nameBox);
        stack.Children.Add(catBox);
        stack.Children.Add(freqBox);
        stack.Children.Add(descBox);
        dialog.Content = stack;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

            var newComp = new Competency
            {
                Id = Guid.NewGuid(),
                Code = codeBox.Text,
                Name = nameBox.Text,
                Category = catBox.SelectedItem?.ToString(),
                RequiredFrequencyMonths = (int)freqBox.Value,
                Description = descBox.Text,
                CreatedAt = DateTime.UtcNow
            };
            await _competencyService.UpsertCompetencyAsync(newComp);
            await LoadCompetenciesAsync();
        }
    }

    private async void EditCompetency_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCompetency == null) return;
        var comp = _selectedCompetency; // Copy ref

        var dialog = new ContentDialog
        {
            Title = "Editar Competencia",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var codeBox = new TextBox { Header = "Código", Text = comp.Code };
        var nameBox = new TextBox { Header = "Nombre", Text = comp.Name };
        var catBox = new ComboBox { Header = "Categoría", HorizontalAlignment = HorizontalAlignment.Stretch, IsEditable = true };
        catBox.Items.Add("General"); catBox.Items.Add("Técnica"); catBox.Items.Add("Seguridad");
        catBox.Text = comp.Category ?? "";
        
        var freqBox = new NumberBox { Header = "Frecuencia (Meses)", Value = comp.RequiredFrequencyMonths ?? 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var descBox = new TextBox { Header = "Descripción", Text = comp.Description ?? "", AcceptsReturn = true, Height = 60 };

        stack.Children.Add(codeBox);
        stack.Children.Add(nameBox);
        stack.Children.Add(catBox);
        stack.Children.Add(freqBox);
        stack.Children.Add(descBox);
        dialog.Content = stack;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            comp.Code = codeBox.Text;
            comp.Name = nameBox.Text;
            comp.Category = catBox.Text; // Allow custom categories
            comp.RequiredFrequencyMonths = freqBox.Value > 0 ? (int)freqBox.Value : null;
            comp.Description = descBox.Text;
            comp.UpdatedAt = DateTime.UtcNow;

            await _competencyService.UpsertCompetencyAsync(comp);
            await LoadCompetenciesAsync();
        }
    }

    private async void DeleteCompetency_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCompetency == null) return;

        var dialog = new ContentDialog
        {
            Title = "Confirmar Eliminación",
            Content = $"¿Estás seguro de que deseas eliminar '{_selectedCompetency.Name}'? Esto no eliminará las evaluaciones históricas pero desaparecerá del catálogo.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _competencyService.DeleteCompetencyAsync(_selectedCompetency.Id);
            _selectedCompetency = null;
            await LoadCompetenciesAsync();
        }
    }
}
