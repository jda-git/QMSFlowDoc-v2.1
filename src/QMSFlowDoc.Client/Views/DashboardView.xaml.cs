using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class DashboardView : Page, INotifyPropertyChanged
{
    private readonly IDashboardService _dashboardService;
    private DashboardStatsDto _stats = new DashboardStatsDto();
    
    public DashboardStatsDto Stats
    {
        get => _stats;
        set { _stats = value; OnPropertyChanged(); }
    }

    public ObservableCollection<DashboardRecentActivityDto> RecentActivity { get; } = new();

    public DashboardView()
    {
        this.InitializeComponent();
        _dashboardService = ((App)Application.Current).DashboardService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadDashboard();
    }

    private async Task LoadDashboard()
    {
        try
        {
            var data = await _dashboardService.GetDashboardDataAsync();
            if (data != null)
            {
                Stats = data.Stats;
                RecentActivity.Clear();
                foreach (var activity in data.RecentActivity)
                {
                    RecentActivity.Add(activity);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading dashboard: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
