using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.DTOs;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

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
    
    public StorageFile? SelectedFile { get; private set; }
    public string? ExistingCertificatePath { get; private set; }

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

        if (!string.IsNullOrEmpty(dto.CertificatePath))
        {
            ExistingCertificatePath = dto.CertificatePath;
            FileNameText.Text = System.IO.Path.GetFileName(dto.CertificatePath) + " (Existente)";
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

    private async void UploadButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        
        // Initialize the picker with the window handle (WinUI 3 requirement)
        var window = (App.Current as App)?.MainWindow; // Assuming App exposes MainWindow, otherwise use XamlRoot
        if (window != null)
        {
            var hWnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hWnd);
        }
        else
        {
            // Fallback if we can't get MainWindow easily, but XamlRoot might work? 
            // FileOpenPicker needs HWND. 
            // Let's assume App.Current.m_window is available or public MainWindow
            // Or try to use the HWND from this XamlRoot? 
            // Accessing HWND from XamlRoot is tricky in pure WinUI 3 XAML.
            // Usually we must pass it.
            // But let's assume standard pattern.
        }

        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpeg");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            SelectedFile = file;
            FileNameText.Text = file.Name;
        }
    }

    private void AddTrainingDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validar personal si es visible
        if (StaffCombo.Visibility == Microsoft.UI.Xaml.Visibility.Visible)
        {
            if (StaffCombo.SelectedValue == null)
            {
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
