using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class PrintOptionsDialog : ContentDialog
{
    private readonly IEnumerable<ReagentListDto> _allReagents;
    
    // Result property
    public PrintOptionsResult? Result { get; private set; }

    // Helpers for Order List
    public ObservableCollection<OrderItemDisplay> OrderItems { get; } = new();
    private ReagentListDto? _selectedReportReagent;

    public PrintOptionsDialog(IEnumerable<ReagentListDto> reagents)
    {
        this.InitializeComponent();
        _allReagents = reagents;
        
        // Init Order List with low stock items
        foreach(var r in _allReagents.Where(x => x.TotalStock <= x.MinStock))
        {
            OrderItems.Add(new OrderItemDisplay(r.Id, r.Name, r.Reference, r.TotalStock, r.Fluorescence, r.InternalCode, r.Manufacturer));
        }
        OrderList.ItemsSource = OrderItems;
        
        // Defaults
        var now = DateTime.Now;
        var firstDay = new DateTime(now.Year, now.Month, 1);
        ConsumStart.Date = firstDay;
        ConsumEnd.Date = now;
        EntryStart.Date = firstDay;
        EntryEnd.Date = now;
        
        this.PrimaryButtonClick += PrintOptionsDialog_PrimaryButtonClick;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag.ToString();
            InventoryPanel.Visibility = tag == "Inventory" ? Visibility.Visible : Visibility.Collapsed;
            OrderPanel.Visibility = tag == "Order" ? Visibility.Visible : Visibility.Collapsed;
            ConsumptionPanel.Visibility = tag == "Consumption" ? Visibility.Visible : Visibility.Collapsed;
            EntriesPanel.Visibility = tag == "Entries" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Logic for Order Search
    private void OrderSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text.ToLower();
            sender.ItemsSource = _allReagents
                .Where(r => r.Name.ToLower().Contains(query) || 
                           (r.InternalCode?.ToLower().Contains(query) ?? false) ||
                           (r.Manufacturer?.ToLower().Contains(query) ?? false))
                .Take(10)
                .ToList();
        }
    }

    private void OrderSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        // DisplayMemberPath handles text update
    }

    private void AddToOrder_Click(object sender, RoutedEventArgs e)
    {
        var text = OrderSearchBox.Text;
        // Try to find exact match by name or the one that was suggested
        var match = _allReagents.FirstOrDefault(r => r.Name.Equals(text, StringComparison.OrdinalIgnoreCase));
        
        if (match != null)
        {
            if (!OrderItems.Any(i => i.Id == match.Id))
                OrderItems.Add(new OrderItemDisplay(match.Id, match.Name, match.Reference, match.TotalStock, match.Fluorescence, match.InternalCode, match.Manufacturer));
            
            OrderSearchBox.Text = "";
        }
    }

    private void RemoveFromOrder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is OrderItemDisplay item)
        {
            OrderItems.Remove(item);
        }
    }


    // Logic for Report Search (Consumption/Entries share logic)
    private void ConsumScope_Changed(object sender, SelectionChangedEventArgs e)
    {
        var rb = (sender as RadioButtons)?.SelectedItem as RadioButton;
        var isSingle = rb?.Tag?.ToString() == "Single";
        ConsumSearchBox.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;
        ConsumSelectedText.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EntryScope_Changed(object sender, SelectionChangedEventArgs e)
    {
        var rb = (sender as RadioButtons)?.SelectedItem as RadioButton;
        var isSingle = rb?.Tag?.ToString() == "Single";
        EntrySearchBox.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed;
        EntrySelectedText.Visibility = isSingle ? Visibility.Visible : Visibility.Collapsed; 
    }

    private void ReportSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text.ToLower();
            sender.ItemsSource = _allReagents
                .Where(r => r.Name.ToLower().Contains(query) || 
                           (r.InternalCode?.ToLower().Contains(query) ?? false) ||
                           (r.Manufacturer?.ToLower().Contains(query) ?? false))
                .Take(10)
                .ToList();
        }
    }

    private void ReportSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is ReagentListDto selected)
        {
            _selectedReportReagent = selected;
            // Text updated by TextMemberPath
            
            // Update label
            if (sender == ConsumSearchBox) ConsumSelectedText.Text = $"Seleccionado: {selected.Name} ({selected.Reference})";
            else EntrySelectedText.Text = $"Seleccionado: {selected.Name} ({selected.Reference})";
        }
    }

    private void PrintOptionsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var navItem = NavView.SelectedItem as NavigationViewItem;
        var mode = navItem?.Tag?.ToString() ?? "Inventory";
        
        Result = new PrintOptionsResult
        {
            Mode = mode,
            // Inventory Params
            SortBy = (SortCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(),
            SortAscending = (OrderRadio.SelectedItem as RadioButton)?.Content?.ToString()?.StartsWith("Asc") ?? true,
            
            // Order Params
            OrderIds = OrderItems.Select(x => x.Id).ToList(),
            
            // Report Params
            StartDate = mode == "Consumption" ? ConsumStart.Date?.DateTime : EntryStart.Date?.DateTime,
            EndDate = mode == "Consumption" ? ConsumEnd.Date?.DateTime : EntryEnd.Date?.DateTime,
            ReagentId = _selectedReportReagent?.Id
        };
        
        // Scope Check
        bool isSingle = false;
        if (mode == "Consumption") isSingle = (ConsumScopeRadio.SelectedItem as RadioButton)?.Tag?.ToString() == "Single";
        if (mode == "Entries") isSingle = (EntryScopeRadio.SelectedItem as RadioButton)?.Tag?.ToString() == "Single";
        
        if (!isSingle) Result.ReagentId = null; // Clear if 'All' selected
    }
}

public class PrintOptionsResult
{
    public string? Mode { get; set; }
    public string? SortBy { get; set; }
    public bool SortAscending { get; set; }
    public List<Guid> OrderIds { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? ReagentId { get; set; }
}

public class OrderItemDisplay
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Reference { get; set; }
    public decimal CurrentStock { get; set; }
    public string Fluorescence { get; set; }
    public string InternalCode { get; set; }
    public string Manufacturer { get; set; }
    
    public OrderItemDisplay(Guid id, string name, string reference, decimal stock, string? fluorescence, string? internalCode, string? manufacturer)
    {
        Id = id; Name = name; Reference = reference; CurrentStock = stock;
        Fluorescence = fluorescence ?? ""; InternalCode = internalCode ?? ""; Manufacturer = manufacturer ?? "";
    }
}
