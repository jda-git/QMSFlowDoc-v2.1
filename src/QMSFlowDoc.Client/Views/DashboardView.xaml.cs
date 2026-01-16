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
            LoadingBar.Visibility = Visibility.Visible;
            StatsGrid.Opacity = 0.5;

            var data = await _dashboardService.GetDashboardDataAsync();
            if (data != null)
            {
                Stats = data.Stats;
                RecentActivity.Clear();
                foreach (var activity in data.RecentActivity)
                {
                    RecentActivity.Add(activity);
                }
                
                // Load Alerts
                await LoadAlertsAsync(data.Stats);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading dashboard: {ex.Message}");
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            StatsGrid.Opacity = 1.0;
        }
    }


    private async Task LoadAlertsAsync(DashboardStatsDto stats)
    {
        var alerts = new List<DashboardAlert>();

        if (stats.PendingReviewDocs > 0)
            alerts.Add(new DashboardAlert("Documentos", $"{stats.PendingReviewDocs} documentos pendientes de revisión"));
        
        if (stats.LowStockReagents > 0)
            alerts.Add(new DashboardAlert("Inventario", $"{stats.LowStockReagents} reactivos con stock bajo"));
        
        if (stats.ExpiringReagents > 0)
            alerts.Add(new DashboardAlert("Caducidad", $"{stats.ExpiringReagents} reactivos próximos a caducar (60 días)"));
        
        if (stats.DueEquipmentMaintenance > 0)
            alerts.Add(new DashboardAlert("Equipos", $"{stats.DueEquipmentMaintenance} mantenimientos pendientes"));
        
        if (stats.PendingEQAResults > 0)
            alerts.Add(new DashboardAlert("EQA", $"{stats.PendingEQAResults} resultados de EQA pendientes"));
        
        if (stats.ExpiringAuthorizations > 0)
            alerts.Add(new DashboardAlert("Métodos", $"{stats.ExpiringAuthorizations} autorizaciones próximas a expirar"));
        
        if (stats.OpenHighRisks > 0)
            alerts.Add(new DashboardAlert("Riesgos", $"{stats.OpenHighRisks} riesgos críticos abiertos"));

        AlertsList.ItemsSource = alerts;
        NoAlertsText.Visibility = alerts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AlertsList.Visibility = alerts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }


    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

public record DashboardAlert(string Title, string Description);

