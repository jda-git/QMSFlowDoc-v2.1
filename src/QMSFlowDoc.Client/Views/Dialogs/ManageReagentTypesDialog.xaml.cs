using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class ManageReagentTypesDialog : ContentDialog
{
    private List<ReagentType> _types = new();

    public ManageReagentTypesDialog()
    {
        this.InitializeComponent();
        this.Loaded += ManageReagentTypesDialog_Loaded;
    }

    private async void ManageReagentTypesDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        var service = ((App)Application.Current).ConfigurationService;
        var types = await service.GetReagentTypesAsync();
        _types = new List<ReagentType>(types);
        ReagentTypesList.ItemsSource = _types;
    }

    private async void AddReagentType_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewTypeBox.Text)) return;

        string name = NewTypeBox.Text.Trim();
        
        // Check for duplicates
        if (_types.Exists(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            var msg = new ContentDialog
            {
                Title = "Error",
                Content = $"El tipo '{name}' ya existe.",
                CloseButtonText = "Aceptar",
                XamlRoot = this.XamlRoot
            };
            await msg.ShowAsync();
            return;
        }

        var service = ((App)Application.Current).ConfigurationService;
        var newType = new ReagentType { Name = name };
        
        var result = await service.CreateReagentTypeAsync(newType);
        if (result != null)
        {
            NewTypeBox.Text = "";
            await LoadData();
        }
    }

    private async void DeleteType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
             var service = ((App)Application.Current).ConfigurationService;
             if (await service.DeleteReagentTypeAsync(id))
             {
                 await LoadData();
             }
        }
    }
}
