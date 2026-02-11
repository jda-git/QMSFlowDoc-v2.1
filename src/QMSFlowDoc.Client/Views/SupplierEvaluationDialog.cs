using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace QMSFlowDoc.Client.Views;

public sealed partial class SupplierEvaluationDialog : ContentDialog
{
    private readonly Guid _supplierId;
    private readonly string _supplierName;
    private string? _attachmentPath;

    public SupplierEvaluationDialog(Guid supplierId, string supplierName)
    {
        _supplierId = supplierId;
        _supplierName = supplierName;
        
        Title = $"Evaluar Proveedor: {supplierName}";
        PrimaryButtonText = "Guardar Evaluación";
        CloseButtonText = "Cancelar";
        DefaultButton = ContentDialogButton.Primary;

        Content = CreateContent();
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private Slider _sliderPlazos = null!;
    private Slider _sliderCalidad = null!;
    private Slider _sliderServicio = null!;
    private Slider _sliderIncidencias = null!;
    private TextBox _periodBox = null!;
    private TextBox _observationsBox = null!;
    private CheckBox _notApprovedCheck = null!;
    private TextBlock _averageLabel = null!;
    private TextBlock _attachmentLabel = null!;
    private DatePicker _datePicker = null!;

    private StackPanel CreateContent()
    {
        var panel = new StackPanel { Spacing = 16, Width = 450 };

        // Date picker
        _datePicker = new DatePicker { Header = "Fecha de Evaluación", Date = DateTimeOffset.Now };
        panel.Children.Add(_datePicker);

        // Period
        _periodBox = new TextBox { Header = "Período Evaluado", PlaceholderText = "Ej: 2024-2025" };
        panel.Children.Add(_periodBox);

        // Score Sliders
        panel.Children.Add(new TextBlock { Text = "Puntuaciones (1-5)", FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(0, 8, 0, 0) });

        _sliderPlazos = CreateScoreSlider("Cumplimiento de Plazos");
        panel.Children.Add(_sliderPlazos);

        _sliderCalidad = CreateScoreSlider("Calidad del Producto/Servicio");
        panel.Children.Add(_sliderCalidad);

        _sliderServicio = CreateScoreSlider("Servicio Técnico/Post-venta");
        panel.Children.Add(_sliderServicio);

        _sliderIncidencias = CreateScoreSlider("Gestión de Incidencias");
        panel.Children.Add(_sliderIncidencias);

        // Average display
        _averageLabel = new TextBlock { Text = "Promedio: 3.0", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 16, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(_averageLabel);

        // Manual NOT Approved checkbox
        _notApprovedCheck = new CheckBox { Content = "Marcar como NO APTO (anula puntuación)", Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(_notApprovedCheck);

        // Observations
        _observationsBox = new TextBox { Header = "Observaciones", PlaceholderText = "Justificación de la decisión...", TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 80 };
        panel.Children.Add(_observationsBox);

        // Attachment
        var attachPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var attachBtn = new Button { Content = "Adjuntar PDF" };
        attachBtn.Click += AttachPdf_Click;
        _attachmentLabel = new TextBlock { Text = "Sin adjunto", VerticalAlignment = VerticalAlignment.Center, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) };
        attachPanel.Children.Add(attachBtn);
        attachPanel.Children.Add(_attachmentLabel);
        panel.Children.Add(attachPanel);

        return panel;
    }

    private Slider CreateScoreSlider(string header)
    {
        var slider = new Slider
        {
            Header = header,
            Minimum = 1,
            Maximum = 5,
            Value = 3,
            StepFrequency = 1,
            TickFrequency = 1,
            Width = 400
        };
        slider.ValueChanged += (s, e) => UpdateAverage();
        return slider;
    }

    private void UpdateAverage()
    {
        var avg = (_sliderPlazos.Value + _sliderCalidad.Value + _sliderServicio.Value + _sliderIncidencias.Value) / 4.0;
        _averageLabel.Text = $"Promedio: {avg:F1}";
        
        if (avg < 3.0)
        {
            _averageLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
        else if (avg < 4.0)
        {
            _averageLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
        }
        else
        {
            _averageLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
    }

    private async void AttachPdf_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".pdf");
        
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)Application.Current).MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            // Copy to local documents folder
            var localFolder = ApplicationData.Current.LocalFolder;
            var destFolder = await localFolder.CreateFolderAsync("SupplierEvaluations", CreationCollisionOption.OpenIfExists);
            var destFile = await file.CopyAsync(destFolder, $"{Guid.NewGuid()}_{file.Name}", NameCollisionOption.ReplaceExisting);
            
            _attachmentPath = destFile.Path;
            _attachmentLabel.Text = file.Name;
            _attachmentLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Defer the click to allow async save
        var deferral = args.GetDeferral();
        
        try
        {
            var request = new CreateSupplierEvaluationRequest(
                _supplierId,
                _datePicker.Date.DateTime,
                _periodBox.Text,
                (int)_sliderPlazos.Value,
                (int)_sliderCalidad.Value,
                (int)_sliderServicio.Value,
                (int)_sliderIncidencias.Value,
                !_notApprovedCheck.IsChecked ?? true, // Inverted: NOT checked = approved
                _observationsBox.Text,
                _attachmentPath
            );

            var app = (App)Application.Current;
            var userId = app.AuthService.CurrentUserId;
            await app.SupplierService.CreateEvaluationAsync(request, userId);
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            // Show error in the dialog content area - not ideal but prevents crash
            System.Diagnostics.Debug.WriteLine($"Evaluation error: {ex}");
        }
        finally
        {
            deferral.Complete();
        }
    }
}
