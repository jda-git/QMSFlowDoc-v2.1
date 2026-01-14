using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.Models;
using System;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class AssessCompetencyDialog : ContentDialog
{
    // Propiedades públicas para obtener los valores ingresados
    public string CompetencyName { get; private set; } = string.Empty;
    public string Area { get; private set; } = string.Empty;
    public CompetencyOutcome Outcome { get; private set; }
    public DateTime EvaluationDate { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public string Evidence { get; private set; } = string.Empty;

    public AssessCompetencyDialog()
    {
        this.InitializeComponent();
        
        // Establecer fecha por defecto a hoy
        EvalDate.Date = DateTimeOffset.Now;
        
        this.PrimaryButtonClick += AssessCompetencyDialog_PrimaryButtonClick;
    }

    private void AssessCompetencyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validar nombre de competencia obligatorio
        if (string.IsNullOrWhiteSpace(CompetencyNameBox.Text))
        {
            args.Cancel = true;
            return;
        }
        
        if (string.IsNullOrWhiteSpace(AreaBox.Text))
        {
            args.Cancel = true;
            return;
        }

        if (!EvalDate.Date.HasValue)
        {
            args.Cancel = true;
            return;
        }

        // Obtener valores
        CompetencyName = CompetencyNameBox.Text.Trim();
        Area = AreaBox.Text.Trim();
        EvaluationDate = EvalDate.Date.Value.DateTime;
        
        var outcomeTag = (OutcomeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        Outcome = outcomeTag switch
        {
            "PASS" => CompetencyOutcome.PASS,
            "FAIL" => CompetencyOutcome.FAIL,
            _ => CompetencyOutcome.CONDITIONAL
        };

        if (ExpiryDate.Date.HasValue)
        {
            ValidUntil = ExpiryDate.Date.Value.DateTime;
        }

        Evidence = EvidenceBox.Text;
    }
}
