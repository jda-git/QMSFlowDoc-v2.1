using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.DTOs;
using System;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class GrantAuthorizationDialog : ContentDialog
{
    // Propiedades públicas para obtener los valores ingresados
    public string TaskName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidUntil { get; private set; }

    public void LoadData(StaffAuthorizationDto dto)
    {
        TaskNameBox.Text = dto.AuthorizationName;
        DescriptionBox.Text = dto.Description;
        FromDate.Date = dto.ValidFrom;
        if (dto.ValidUntil.HasValue) UntilDate.Date = dto.ValidUntil.Value;
        
        this.Title = "Editar Autorización";
        this.PrimaryButtonText = "Guardar";
        this.SecondaryButtonText = "Eliminar"; // Enable Delete button
    }

    public GrantAuthorizationDialog()
    {
        this.InitializeComponent();
        
        // Establecer fecha por defecto a hoy
        FromDate.Date = DateTimeOffset.Now;
        
        this.PrimaryButtonClick += GrantAuthorizationDialog_PrimaryButtonClick;
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

    private void StaffCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Future: Filter competencies based on staff?
    }

    private void CompetencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
         if (CompetencyCombo.SelectedItem is CompetencyDto comp)
         {
             if (string.IsNullOrWhiteSpace(TaskNameBox.Text))
             {
                 TaskNameBox.Text = $"Autorización para: {comp.Name}";
             }
         }
    }

    private void GrantAuthorizationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
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

        // Validar nombre de tarea obligatorio
        if (string.IsNullOrWhiteSpace(TaskNameBox.Text))
        {
            TaskNameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            args.Cancel = true;
            return;
        }
        
        if (!FromDate.Date.HasValue)
        {
            FromDate.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            args.Cancel = true;
            return;
        }

        // Obtener valores
        TaskName = TaskNameBox.Text.Trim();
        Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
        ValidFrom = FromDate.Date.Value.DateTime;
        
        if (UntilDate.Date.HasValue)
        {
            ValidUntil = UntilDate.Date.Value.DateTime;
        }
    }
}
