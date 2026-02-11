using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class SupplierEditorView : Page
{
    private Guid? _supplierId;
    private SupplierDetailDto? _supplier;

    public SupplierEditorView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is Guid id)
        {
            _supplierId = id;
            PageTitle.Text = "Editar Proveedor";
            await LoadSupplierAsync(id);
        }
    }

    private async Task LoadSupplierAsync(Guid id)
    {
        var app = (App)Application.Current;
        _supplier = await app.SupplierService.GetSupplierByIdAsync(id);
        
        if (_supplier == null)
        {
            await ShowMessage("Error", "Proveedor no encontrado.");
            Frame.GoBack();
            return;
        }

        // Populate fields
        NameBox.Text = _supplier.Name;
        TypeCombo.SelectedIndex = (int)_supplier.Type;
        ContactBox.Text = _supplier.ContactName ?? "";
        EmailBox.Text = _supplier.Email ?? "";
        PhoneBox.Text = _supplier.Phone ?? "";
        AddressBox.Text = _supplier.Address ?? "";
        NotesBox.Text = _supplier.Notes ?? "";

        // Status badge
        StatusBadge.Text = $"Estado: {_supplier.QualityStatus}";
        StatusBadge.Foreground = SuppliersView.GetStatusColor(_supplier.QualityStatus);

        // Evaluations list
        EvaluationsList.ItemsSource = _supplier.Evaluations;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            await ShowMessage("Validación", "El nombre del proveedor es obligatorio.");
            return;
        }

        var app = (App)Application.Current;

        if (_supplierId.HasValue && _supplier != null)
        {
            // Update existing
            _supplier.Name = NameBox.Text;
            _supplier.Type = (SupplierType)TypeCombo.SelectedIndex;
            _supplier.ContactName = ContactBox.Text;
            _supplier.Email = EmailBox.Text;
            _supplier.Phone = PhoneBox.Text;
            _supplier.Address = AddressBox.Text;
            _supplier.Notes = NotesBox.Text;

            await app.SupplierService.UpdateSupplierAsync(_supplier);
        }
        else
        {
            // Create new
            var request = new CreateSupplierRequest(
                NameBox.Text,
                ContactBox.Text,
                EmailBox.Text,
                PhoneBox.Text,
                AddressBox.Text,
                NotesBox.Text,
                (SupplierType)TypeCombo.SelectedIndex
            );

            await app.SupplierService.CreateSupplierAsync(request);
        }

        Frame.GoBack();
    }

    private async void NewEvaluation_Click(object sender, RoutedEventArgs e)
    {
        if (!_supplierId.HasValue)
        {
            await ShowMessage("Guardar primero", "Guarde el proveedor antes de crear evaluaciones.");
            return;
        }

        var dialog = new SupplierEvaluationDialog(_supplierId.Value, NameBox.Text)
        {
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await LoadSupplierAsync(_supplierId.Value);
        }
    }

    private async Task ShowMessage(string title, string content)
    {
        var dlg = new ContentDialog { Title = title, Content = content, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
        await dlg.ShowAsync();
    }
}
