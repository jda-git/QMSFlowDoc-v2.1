using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;

namespace QMSFlowDoc.Client.Views;

public sealed partial class RiskEditorView : Page
{
    private Guid? _riskId;

    public RiskEditorView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            _riskId = id;
            await LoadRisk(id);
        }
    }

    private async System.Threading.Tasks.Task LoadRisk(Guid id)
    {
        try
        {
            var service = ((App)Application.Current).ImprovementService;
            var risk = await service.GetRiskByIdAsync(id);
            if (risk != null)
            {
                TitleBox.Text = risk.Title;
                DescriptionBox.Text = risk.Description;
                OwnerBox.Text = ""; // Risk model has OwnerUserId (Guid) but UI expects name. Skipped for now or fetch user name.
                MitigationBox.Text = risk.MitigationPlan;

                // Select Category
                foreach (ComboBoxItem item in CategoryCombo.Items)
                {
                    if (item.Content.ToString() == risk.Category)
                    {
                        CategoryCombo.SelectedItem = item;
                        break;
                    }
                }

                // Select Likelihood
                foreach (ComboBoxItem item in LikelihoodCombo.Items)
                {
                    if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tagVal) && tagVal == (int)risk.Likelihood)
                    {
                        LikelihoodCombo.SelectedItem = item;
                        break;
                    }
                }

                // Select Impact
                foreach (ComboBoxItem item in ImpactCombo.Items)
                {
                    if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tagVal) && tagVal == (int)risk.Impact)
                    {
                        ImpactCombo.SelectedItem = item;
                        break;
                    }
                }

                // Select Status
                foreach (ComboBoxItem item in StatusCombo.Items)
                {
                    if (item.Content.ToString() == risk.Status.ToString())
                    {
                        StatusCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
             ErrorText.Text = $"Error loading risk: {ex.Message}";
             ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            ErrorText.Text = "El título del riesgo es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var service = ((App)Application.Current).ImprovementService;
            var likelihood = (RiskLikelihood)(int.TryParse((LikelihoodCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out int l) ? l : 3);
            var impact = (RiskImpact)(int.TryParse((ImpactCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out int i) ? i : 3);
            var category = (CategoryCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Técnico";

            var request = new CreateRiskRequest(
                TitleBox.Text,
                DescriptionBox.Text,
                category,
                likelihood,
                impact,
                MitigationBox.Text,
                null // OwnerUserId
            );

            if (_riskId.HasValue)
            {
                // Update
                var success = await service.UpdateRiskAsync(_riskId.Value, request);
                
                // Update Status
                var statusStr = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (Enum.TryParse<RiskStatus>(statusStr, out var statusEnum))
                {
                    await service.UpdateRiskStatusAsync(_riskId.Value, (int)statusEnum);
                }

                if (success) Frame.GoBack();
                else { ErrorText.Text = "Error al actualizar."; ErrorText.Visibility = Visibility.Visible; }
            }
            else
            {
                // Create
                var result = await service.CreateRiskAsync(request);
                if (result != null)
                {
                    Frame.GoBack();
                }
                else
                {
                    ErrorText.Text = "Error al guardar el riesgo.";
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
