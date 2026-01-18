using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.DTOs;
using System;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class AddTrainingDialog : ContentDialog
{
    // Propiedades públicas para obtener los valores ingresados
    public string TrainingTitle { get; private set; } = string.Empty;
    public string? Provider { get; private set; }
    public decimal Hours { get; private set; }
    public DateTime CompletedAt { get; private set; }
    public string Result { get; private set; } = "APTO";
    public string? Notes { get; private set; }

    public void LoadData(StaffTrainingDto dto)
    {
        TitleBox.Text = dto.Title;
        ProviderBox.Text = dto.Provider;
        HoursBox.Text = dto.Hours.ToString();
        if (dto.CompletionDate > DateTime.MinValue)
        {
            CompletionDatePicker.Date = dto.CompletionDate;
        }
        else
        {
            CompletionDatePicker.Date = null;
        }
        
        foreach(ComboBoxItem item in ResultCombo.Items)
        {
            if (item.Tag?.ToString() == dto.Result)
            {
                ResultCombo.SelectedItem = item;
                break;
            }
        }
        
        if (dto.CompetencyId.HasValue)
        {
            CompetencyCombo.SelectedValue = dto.CompetencyId.Value;
        }

        this.Title = "Editar Formación";
        this.PrimaryButtonText = "Guardar";
        this.SecondaryButtonText = "Eliminar"; // Enable Delete button
    }

    public AddTrainingDialog()
    {
        this.InitializeComponent();
        
        // Establecer fecha por defecto a hoy
        CompletionDatePicker.Date = DateTimeOffset.Now;
        
        this.PrimaryButtonClick += AddTrainingDialog_PrimaryButtonClick;
    }

    public string? SelectedStaffId { get; private set; }

    public void EnableStaffSelection(System.Collections.Generic.IEnumerable<StaffListDto> staffList)
    {
        StaffCombo.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        StaffCombo.ItemsSource = staffList;
    }

    public Guid? SelectedCompetencyId { get; private set; }

    public void LoadCompetencies(System.Collections.Generic.IEnumerable<CompetencyDto> competencies)
    {
        CompetencyCombo.ItemsSource = competencies;
    }

    private void CompetencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
         if (CompetencyCombo.SelectedItem is CompetencyDto comp)
         {
             if (string.IsNullOrWhiteSpace(TitleBox.Text))
             {
                 TitleBox.Text = $"Formación: {comp.Name}";
             }
         }
    }

    private void AddTrainingDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validar personal si es visible
        if (StaffCombo.Visibility == Microsoft.UI.Xaml.Visibility.Visible)
        {
            if (StaffCombo.SelectedValue == null)
            {
                // Show error or just cancel? ContentDialog doesn't easily support inline error without extra UI.
                // For now, we rely on user seeing it empty. Maybe focus it.
                StaffCombo.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                args.Cancel = true;
                return;
            }
            SelectedStaffId = StaffCombo.SelectedValue.ToString();
        }

        if (CompetencyCombo.SelectedValue != null)
        {
            if (Guid.TryParse(CompetencyCombo.SelectedValue.ToString(), out var compId))
            {
                SelectedCompetencyId = compId;
            }
        }

        // Validar título obligatorio
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            TitleBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            args.Cancel = true;
            return;
        }

        if (!CompletionDatePicker.Date.HasValue)
        {
            CompletionDatePicker.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            args.Cancel = true;
            return;
        }

        // Obtener valores
        TrainingTitle = TitleBox.Text.Trim();
        Provider = string.IsNullOrWhiteSpace(ProviderBox.Text) ? null : ProviderBox.Text.Trim();
        
        // Parsear horas (por defecto 0 si no es válido)
        if (decimal.TryParse(HoursBox.Text, out var hours))
        {
            Hours = hours;
        }

        CompletedAt = CompletionDatePicker.Date.Value.DateTime;
        
        var resultItem = ResultCombo.SelectedItem as ComboBoxItem;
        Result = resultItem?.Tag?.ToString() ?? "APTO";
        
        Notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text;
    }
}
