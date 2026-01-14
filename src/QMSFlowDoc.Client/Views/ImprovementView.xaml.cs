using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ImprovementView : Page
{
    private readonly IImprovementService _improvementService;
    
    public ObservableCollection<RiskListDto> Risks { get; } = new();
    public ObservableCollection<AuditListDto> Audits { get; } = new();
    public ObservableCollection<ManagementReviewListDto> Reviews { get; } = new();

    private List<RiskListDto> _allRisks = new();

    public ImprovementView()
    {
        this.InitializeComponent();
        _improvementService = ((App)Application.Current).ImprovementService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAllData();
    }

    private async Task LoadAllData()
    {
        try
        {
            var risks = await _improvementService.GetRisksAsync();
            _allRisks = risks.ToList();
            ApplyRiskFilter();

            var audits = await _improvementService.GetAuditsAsync();
            Audits.Clear();
            foreach (var a in audits) Audits.Add(a);

            var reviews = await _improvementService.GetReviewsAsync();
            Reviews.Clear();
            foreach (var r in reviews) Reviews.Add(r);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading improvement data: {ex.Message}");
        }
    }

    private void ApplyRiskFilter()
    {
        Risks.Clear();
        foreach (var r in _allRisks)
        {
            if (ActiveRisksFilter.IsOn && r.Status != RiskStatus.ACTIVE) continue;
            Risks.Add(r);
        }
    }

    private void Filter_Toggled(object sender, RoutedEventArgs e)
    {
        ApplyRiskFilter();
    }

    private void AddRisk_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(RiskEditorView));
    }

    private void RisksList_ItemClick(object sender, ItemClickEventArgs e)
    {
         if (e.ClickedItem is RiskListDto risk)
        {
            Frame.Navigate(typeof(RiskEditorView), risk.Id);
        }
    }

    private async void ExportMatrix_Click(object sender, RoutedEventArgs e)
    {
        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("CSV", new List<string>() { ".csv" });
        savePicker.SuggestedFileName = "MatrizRiesgos";
        
        // Initialize the picker with the window handle (WinUI 3 requirement)
        var window = (Application.Current as App)?.MainWindow;
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            var lines = new List<string> { "Titulo,Categoria,Probabilidad,Impacto,Score,Estado" };
            foreach (var r in _allRisks)
            {
                lines.Add($"\"{r.Title}\",\"{r.Category}\",{r.Likelihood},{r.Impact},{r.Score},{r.Status}");
            }
            await Windows.Storage.FileIO.WriteLinesAsync(file, lines);
        }
    }

    private void AuditsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AuditListDto audit)
        {
            Frame.Navigate(typeof(AuditEditorView), audit.Id);
        }
    }

    private void AddAudit_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(AuditEditorView));
    }

    private void AddReview_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ReviewEditorView));
    }
}
