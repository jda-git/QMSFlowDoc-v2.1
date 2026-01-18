using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class EvaluateCompetencyDialog : ContentDialog
{
    public string SelectedStaffId => StaffCombo.SelectedValue?.ToString();
    public string SelectedCompetencyId => CompetencyCombo.SelectedValue?.ToString();
    public DateTime EvaluationDate => EvaluationDatePicker.Date.HasValue ? EvaluationDatePicker.Date.Value.DateTime : DateTime.Now;
    public DateTime? ValidUntil => ValidUntilDatePicker.Date.HasValue ? ValidUntilDatePicker.Date.Value.DateTime : null;
    public string Evidence => MethodBox.Text; 
    public string Outcome => (OutcomeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Competent";
    public string Evaluator => EvaluatorBox.Text;
    public string Notes => NotesBox.Text;
    public string Area => _originalDto?.Area;

    private CompetencyEvaluationDto? _originalDto;

    public EvaluateCompetencyDialog()
    {
        this.InitializeComponent();
        EvaluationDatePicker.Date = DateTimeOffset.Now;
    }

    public void LoadStaff(IEnumerable<StaffListDto> staffList)
    {
        StaffCombo.ItemsSource = staffList;
        if (StaffCombo.Items.Count > 0) StaffCombo.SelectedIndex = 0;
    }

    public void LoadCompetencies(IEnumerable<CompetencyDto> compList)
    {
        CompetencyCombo.ItemsSource = compList;
        if (CompetencyCombo.Items.Count > 0) CompetencyCombo.SelectedIndex = 0;
    }

    public void SetFixedContext(Guid staffId, Guid competencyId)
    {
        StaffCombo.SelectedValue = staffId;
        StaffCombo.IsEnabled = false;
        
        CompetencyCombo.SelectedValue = competencyId;
        CompetencyCombo.IsEnabled = false;
    }

    public void LoadData(CompetencyEvaluationDto dto)
    {
        _originalDto = dto;
        
        StaffCombo.SelectedValue = dto.StaffId;
        StaffCombo.IsEnabled = false;

        CompetencyCombo.SelectedValue = dto.CompetencyId;
        CompetencyCombo.IsEnabled = false;
        
        EvaluationDatePicker.Date = dto.EvaluationDate;
        if (dto.ValidUntil.HasValue) ValidUntilDatePicker.Date = dto.ValidUntil.Value;
        
        MethodBox.Text = dto.Evidence ?? "";
        
        foreach (ComboBoxItem item in OutcomeCombo.Items)
        {
            if (item.Tag?.ToString() == dto.Outcome)
            {
                OutcomeCombo.SelectedItem = item;
                break;
            }
        }
        
        EvaluatorBox.Text = dto.EvaluatorName ?? "";
        NotesBox.Text = ""; // Evidence is already in MethodBox. If Notes were separate, we'd use them.
    }
}
