using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Client.Views;

public sealed partial class MethodsView : Page
{
    private readonly IMethodService _methodService;
    private readonly IStaffService _staffService;
    private MethodDto? _selectedMethod;

    public MethodsView()
    {
        this.InitializeComponent();
        _methodService = ((App)Application.Current).MethodService;
        _staffService = ((App)Application.Current).StaffService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadMethodsAsync();
    }

    private async Task LoadMethodsAsync()
    {
        try 
        {
            LoadingBar.Visibility = Visibility.Visible;
            if (MethodList != null) MethodList.Opacity = 0.5;

            var methods = await _methodService.GetMethodsAsync();
            if (MethodList != null) MethodList.ItemsSource = methods;

            if (_selectedMethod != null)
            {
                var reselect = methods.FirstOrDefault(m => m.Id == _selectedMethod.Id);
                if (reselect != null)
                {
                    if (MethodList != null) MethodList.SelectedItem = reselect;
                }
            }
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            if (MethodList != null) MethodList.Opacity = 1.0;
        }
    }


    private async void MethodList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MethodList.SelectedItem is MethodDto method)
        {
            _selectedMethod = method;
            DetailPane.Visibility = Visibility.Visible;
            EmptySelectionPane.Visibility = Visibility.Collapsed;

            // Bind header
            DetailCode.Text = method.Code;
            DetailVersion.Text = method.CurrentVersion ?? "";
            DetailName.Text = method.Name;
            DetailCategory.Text = method.Category ?? "Sin Categoría";

            if (method.DocumentId.HasValue && !string.IsNullOrEmpty(method.DocumentTitle))
            {
                DetailDocLink.Visibility = Visibility.Visible;
                DetailDocTitle.Text = method.DocumentTitle;
                NoDocText.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailDocLink.Visibility = Visibility.Collapsed;
                NoDocText.Visibility = Visibility.Visible;
            }

            ActivateButton.Visibility = method.Status == MethodStatus.DRAFT ? Visibility.Visible : Visibility.Collapsed;

            await LoadAuthorizationsAsync(method.Id);
            await LoadVersionsAsync(method.Id);
            await LoadUncertaintiesAsync(method.Id);
        }
        else
        {
            _selectedMethod = null;
            DetailPane.Visibility = Visibility.Collapsed;
            EmptySelectionPane.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadAuthorizationsAsync(Guid methodId)
    {
        var auths = await _methodService.GetAuthorizationsAsync(methodId);
        AuthorizationsList.ItemsSource = auths;
        EmptyAuthText.Visibility = auths.Any() ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void AddMethod_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Nuevo Método",
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var codeBox = new TextBox { Header = "Código", PlaceholderText = "M-HEM-001" };
        var nameBox = new TextBox { Header = "Nombre", PlaceholderText = "Hemograma Automatizado" };
        var catBox = new ComboBox { Header = "Categoría", HorizontalAlignment = HorizontalAlignment.Stretch };
        catBox.Items.Add("Hematología");
        catBox.Items.Add("Bioquímica");
        catBox.Items.Add("Inmunología");
        catBox.Items.Add("Microbiología");
        catBox.Items.Add("Genética");
        catBox.Items.Add("Otros");
        catBox.SelectedIndex = 0;
        var verBox = new TextBox { Header = "Versión", PlaceholderText = "v1.0" };

        stack.Children.Add(codeBox);
        stack.Children.Add(nameBox);
        stack.Children.Add(catBox);
        stack.Children.Add(verBox);
        dialog.Content = stack;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(codeBox.Text) || string.IsNullOrWhiteSpace(nameBox.Text)) return;
            
            var req = new CreateMethodRequest(codeBox.Text, nameBox.Text, catBox.SelectedItem?.ToString(), verBox.Text, null, null);
            await _methodService.CreateMethodAsync(req);
            await LoadMethodsAsync();
        }
    }

    private async void EditMethod_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMethod == null) return;

        var method = await _methodService.GetMethodByIdAsync(_selectedMethod.Id);
        if (method == null) return;

        var dialog = new ContentDialog
        {
            Title = "Editar Método",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var codeBox = new TextBox { Header = "Código", Text = method.Code };
        var nameBox = new TextBox { Header = "Nombre", Text = method.Name };
        var catBox = new ComboBox { Header = "Categoría", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var cat in new[] { "Hematología", "Bioquímica", "Inmunología", "Microbiología", "Genética", "Otros" })
            catBox.Items.Add(cat);
        catBox.SelectedItem = method.Category;
        var verBox = new TextBox { Header = "Versión", Text = method.CurrentVersion ?? "" };

        stack.Children.Add(codeBox);
        stack.Children.Add(nameBox);
        stack.Children.Add(catBox);
        stack.Children.Add(verBox);
        dialog.Content = stack;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var req = new UpdateMethodRequest(method.Id, codeBox.Text, nameBox.Text, catBox.SelectedItem?.ToString(), method.Status, verBox.Text, method.EffectiveDate, method.DocumentId, method.Notes);
            await _methodService.UpdateMethodAsync(req);
            await LoadMethodsAsync();
        }
    }

    private async void ActivateMethod_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMethod == null) return;
        
        var method = await _methodService.GetMethodByIdAsync(_selectedMethod.Id);
        if (method == null) return;

        var req = new UpdateMethodRequest(method.Id, method.Code, method.Name, method.Category, MethodStatus.ACTIVE, method.CurrentVersion, DateTime.UtcNow, method.DocumentId, method.Notes);
        await _methodService.UpdateMethodAsync(req);
        await LoadMethodsAsync();
    }

    private async void AddAuthorization_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMethod == null) return;

        // Get staff list for selection
        var staff = await _staffService.GetStaffAsync();
        
        var dialog = new ContentDialog
        {
            Title = "Autorizar Usuario",
            PrimaryButtonText = "Autorizar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var userCombo = new ComboBox { Header = "Seleccionar Usuario", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var s in staff)
            userCombo.Items.Add(new ComboBoxItem { Content = s.FullName, Tag = s.Id });
        
        var expiryPicker = new DatePicker { Header = "Fecha de Expiración (Opcional)" };

        stack.Children.Add(userCombo);
        stack.Children.Add(expiryPicker);
        dialog.Content = stack;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (userCombo.SelectedItem is ComboBoxItem item && item.Tag is Guid userId)
            {
                DateTime? expiry = expiryPicker.Date.Year > 1900 ? expiryPicker.Date.DateTime : null;
                var req = new AuthorizeMethodRequest(_selectedMethod.Id, userId, expiry, null);
                await _methodService.AuthorizeUserAsync(req);
                await LoadAuthorizationsAsync(_selectedMethod.Id);
                await LoadMethodsAsync(); // Refresh count
            }
        }
    }

    private async void RemoveAuthorization_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid authId)
        {
            var dialog = new ContentDialog
            {
                Title = "¿Revocar autorización?",
                Content = "El usuario perderá autorización para ejecutar este método.",
                PrimaryButtonText = "Revocar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _methodService.RemoveAuthorizationAsync(authId);
                if (_selectedMethod != null)
                {
                    await LoadAuthorizationsAsync(_selectedMethod.Id);
                    await LoadMethodsAsync();
                }
            }
        }
    }

    private void DetailDocLink_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to document (TODO: Implement document viewer navigation)
    }

    // Static helpers for XAML bindings
    public static SolidColorBrush GetStatusBackground(MethodStatus status)
    {
        return status switch
        {
            MethodStatus.DRAFT => new SolidColorBrush(Colors.Gray),
            MethodStatus.ACTIVE => new SolidColorBrush(Colors.Green),
            MethodStatus.OBSOLETE => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public static string FormatAuthCount(int count) => count == 0 ? "Sin autorizados" : $"{count} autorizados";
    public static string FormatDate(DateTime d) => d.ToString("d");
    public static string FormatExpiry(DateTime? d) => d.HasValue ? $"Expira: {d.Value:d}" : "Sin Expiración";

    private async Task LoadVersionsAsync(Guid methodId)
    {
        var versions = await _methodService.GetVersionsAsync(methodId);
        if (VersionsList != null) VersionsList.ItemsSource = versions;
        if (EmptyVersionsText != null) EmptyVersionsText.Visibility = versions.Any() ? Visibility.Collapsed : Visibility.Visible;

        // Auto-load validations for the latest version if available
        if (versions.FirstOrDefault() is MethodVersionDto latest)
        {
            await LoadValidationsAsync(latest.Id);
        }
        else
        {
            if (ValidationsList != null) ValidationsList.ItemsSource = null;
            if (EmptyValidationsText != null) EmptyValidationsText.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadValidationsAsync(Guid versionId)
    {
        var validations = await _methodService.GetValidationsAsync(versionId);
        if (ValidationsList != null) ValidationsList.ItemsSource = validations;
        if (EmptyValidationsText != null) EmptyValidationsText.Visibility = validations.Any() ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void NewVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMethod == null) return;

        var dialog = new ContentDialog
        {
            Title = "Nueva Versión",
            PrimaryButtonText = "Crear Borrador",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };
        var verBox = new TextBox { Header = "Nueva Versión", PlaceholderText = "v1.1", Text = "v" };
        var descBox = new TextBox { Header = "Descripción del Cambio", PlaceholderText = "Revisión anual...", AcceptsReturn = true, Height = 80 };

        stack.Children.Add(verBox);
        stack.Children.Add(descBox);
        dialog.Content = stack;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(verBox.Text)) return;

            var req = new CreateMethodVersionRequest(_selectedMethod.Id, verBox.Text, descBox.Text, null, "System"); 
            await _methodService.CreateVersionAsync(req);
            await LoadVersionsAsync(_selectedMethod.Id);
        }
    }

    private async void ApproveVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid versionId)
        {
            var dialog = new ContentDialog
            {
                Title = "Aprobar Versión",
                Content = "¿Confirma que desea aprobar esta versión? Se marcará como la versión vigente y las anteriores pasarán a ser obsoletas.",
                PrimaryButtonText = "Aprobar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await _methodService.ApproveVersionAsync(versionId, "System"); 
                if (_selectedMethod != null)
                {
                    await LoadMethodsAsync(); 
                    await LoadVersionsAsync(_selectedMethod.Id);
                }
            }
        }
    }

    private async void AddValidation_Click(object sender, RoutedEventArgs e)
    {
        var versions = VersionsList.ItemsSource as List<MethodVersionDto>;
        var targetVersion = versions?.FirstOrDefault();

        if (targetVersion == null)
        {
             var errDialog = new ContentDialog
            {
                Title = "Error",
                Content = "No hay versiones disponibles para validar.",
                CloseButtonText = "Ok",
                XamlRoot = this.Content.XamlRoot
            };
            await errDialog.ShowAsync();
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Registrar Validación (ISO 15189)",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var stack = new StackPanel { Spacing = 12 };

        // ISO 15189 predefined validation parameters
        var paramCombo = new ComboBox { Header = "Parámetro ISO 15189", HorizontalAlignment = HorizontalAlignment.Stretch };
        var isoParams = new[]
        {
            "Precisión — Repetibilidad (CV%)",
            "Precisión — Reproducibilidad (CV%)",
            "Veracidad / Sesgo (Bias %)",
            "Límite de Detección (LOD)",
            "Límite de Cuantificación (LOQ)",
            "Linealidad (R²)",
            "Incertidumbre de Medida (MU)",
            "Carry-over / Arrastre",
            "Estabilidad de Muestra",
            "Interferencias",
            "Comparación de Métodos",
            "Robustez",
            "Otro (personalizado)"
        };
        foreach (var p in isoParams) paramCombo.Items.Add(p);
        paramCombo.SelectedIndex = 0;

        var customParamBox = new TextBox 
        { 
            Header = "Parámetro personalizado", 
            PlaceholderText = "Especifique el parámetro...",
            Visibility = Visibility.Collapsed 
        };
        paramCombo.SelectionChanged += (s, args) =>
        {
            customParamBox.Visibility = paramCombo.SelectedIndex == isoParams.Length - 1 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        };

        var resBox = new TextBox { Header = "Resultado / Conclusión", PlaceholderText = "Cumple con las especificaciones...", AcceptsReturn = true, Height = 60 };
        var countBox = new NumberBox { Header = "Nº de Experimentos", Value = 1, Minimum = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };

        stack.Children.Add(paramCombo);
        stack.Children.Add(customParamBox);
        stack.Children.Add(resBox);
        stack.Children.Add(countBox);
        dialog.Content = stack;

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            string selectedParam = paramCombo.SelectedIndex == isoParams.Length - 1
                ? customParamBox.Text
                : paramCombo.SelectedItem?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(selectedParam)) return;

            var val = new MethodValidationDto(
                Guid.NewGuid(), 
                targetVersion.Id, 
                selectedParam, 
                resBox.Text, 
                (int)countBox.Value, 
                null, 
                null
            );
            await _methodService.AddValidationAsync(val);
            await LoadValidationsAsync(targetVersion.Id);
        }
    }

    private async void ViewReport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton btn && btn.Tag is string path && !string.IsNullOrEmpty(path))
        {
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
                await Windows.System.Launcher.LaunchFileAsync(file);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error al abrir documento",
                    Content = $"No se pudo abrir el archivo en: {path}\nError: {ex.Message}",
                    CloseButtonText = "Ok",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }

    private async System.Threading.Tasks.Task LoadUncertaintiesAsync(Guid methodId)
    {
        try
        {
            var store = ((App)Application.Current).LocalStore;
            var uncertainties = await store.GetUncertaintiesAsync(methodId);
            UncertaintiesList.Items.Clear();

            if (uncertainties.Count == 0)
            {
                EmptyUncertaintiesText.Visibility = Visibility.Visible;
                UncertaintiesList.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyUncertaintiesText.Visibility = Visibility.Collapsed;
            UncertaintiesList.Visibility = Visibility.Visible;

            foreach (var u in uncertainties)
            {
                var grid = new Grid { Padding = new Thickness(12, 8, 12, 8), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 0, 120, 215)), Margin = new Thickness(0, 4, 0, 0), CornerRadius = new CornerRadius(4) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var analyteText = new TextBlock { Text = u.AnalyteName, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
                Grid.SetColumn(analyteText, 0);
                grid.Children.Add(analyteText);

                var valueText = new TextBlock { Text = $"\u00b1 {u.Value} {u.Unit}" };
                Grid.SetColumn(valueText, 1);
                grid.Children.Add(valueText);

                var kText = new TextBlock { Text = $"k={u.CoverageFactor}", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) };
                Grid.SetColumn(kText, 2);
                grid.Children.Add(kText);

                var confText = new TextBlock { Text = u.ConfidenceLevel, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) };
                Grid.SetColumn(confText, 3);
                grid.Children.Add(confText);

                var dateText = new TextBlock { Text = u.EstimatedDate.ToString("d"), Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) };
                Grid.SetColumn(dateText, 4);
                grid.Children.Add(dateText);

                UncertaintiesList.Items.Add(new ListViewItem { Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading uncertainties: {ex.Message}");
        }
    }

    private async void AddUncertainty_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMethod == null) return;

        var stack = new StackPanel { Spacing = 10 };
        var analyteBox = new TextBox { Header = "Analito/Mensurando", PlaceholderText = "Ej: Glucosa" };
        var valueBox = new NumberBox { Header = "Valor de Incertidumbre", SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var unitBox = new TextBox { Header = "Unidad", PlaceholderText = "Ej: mg/dL, mmol/L, %" };
        var kBox = new NumberBox { Header = "Factor de Cobertura (k)", Value = 2.0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var confBox = new ComboBox { Header = "Nivel de Confianza", Items = { "95%", "99%", "99.7%" }, SelectedIndex = 0 };
        var notesBox = new TextBox { Header = "Notas (Opcional)", PlaceholderText = "Método de estimación, fuentes de incertidumbre..." };

        stack.Children.Add(analyteBox);
        stack.Children.Add(valueBox);
        stack.Children.Add(unitBox);
        stack.Children.Add(kBox);
        stack.Children.Add(confBox);
        stack.Children.Add(notesBox);

        var dialog = new ContentDialog
        {
            Title = "Registrar Incertidumbre de Medida",
            Content = new ScrollViewer { Content = stack, MaxHeight = 400 },
            PrimaryButtonText = "Registrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(analyteBox.Text) || string.IsNullOrWhiteSpace(unitBox.Text))
            {
                var errDlg = new ContentDialog { Title = "Error", Content = "Analito y Unidad son obligatorios.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                await errDlg.ShowAsync();
                return;
            }

            var store = ((App)Application.Current).LocalStore;
            await store.CreateUncertaintyAsync(
                _selectedMethod.Id,
                analyteBox.Text.Trim(),
                valueBox.Value,
                unitBox.Text.Trim(),
                kBox.Value,
                (confBox.SelectedItem as string) ?? "95%",
                string.IsNullOrWhiteSpace(notesBox.Text) ? null : notesBox.Text
            );

            await LoadUncertaintiesAsync(_selectedMethod.Id);
        }
    }
}
