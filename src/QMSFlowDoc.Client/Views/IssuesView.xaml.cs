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

namespace QMSFlowDoc.Client.Views;

public sealed partial class IssuesView : Page
{
    private readonly IQualityService _qualityService;
    public ObservableCollection<NCListDto> Issues { get; } = new();

    private List<NCListDto> _allIssues = new();

    public IssuesView()
    {
        this.InitializeComponent();
        _qualityService = ((App)Application.Current).QualityService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadIssues();
    }

    private async Task LoadIssues()
    {
        try
        {
            var list = await _qualityService.GetNonconformitiesAsync();
            _allIssues = new List<NCListDto>(list);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading issues: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        Issues.Clear();
        var filter = (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();

        foreach (var item in _allIssues)
        {
            if (filter == "Abiertas" && item.Status == NCStatus.CLOSED) continue;
            if (filter == "Cerradas" && item.Status != NCStatus.CLOSED) continue;
            // "Todas" shows all
            
            Issues.Add(item);
        }
    }

    private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void AddNC_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(NCEditorView));
    }

    private void IssuesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NCListDto item)
        {
            Frame.Navigate(typeof(NCEditorView), item.Id);
        }
    }
}
