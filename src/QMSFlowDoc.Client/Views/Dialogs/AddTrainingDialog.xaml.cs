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

    public AddTrainingDialog()
    {
        this.InitializeComponent();
        
        // Establecer fecha por defecto a hoy
        CompletionDatePicker.Date = DateTimeOffset.Now;
        
        this.PrimaryButtonClick += AddTrainingDialog_PrimaryButtonClick;
    }

    private void AddTrainingDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validar título obligatorio
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            args.Cancel = true;
            return;
        }

        if (!CompletionDatePicker.Date.HasValue)
        {
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
