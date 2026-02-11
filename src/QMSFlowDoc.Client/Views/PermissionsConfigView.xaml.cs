using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class PermissionsConfigView : Page
{
    private List<Role> _roles = new();
    private List<Permission> _permissions = new();
    private Dictionary<Guid, HashSet<Guid>> _matrix = new(); // RoleId -> Set of PermissionIds
    private bool _isLoading = false;

    public PermissionsConfigView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        // Security Check
        var authService = ((App)Application.Current).AuthService;
        if (!authService.IsAdmin)
        {
            // Redirect if not admin
            if (Frame.CanGoBack) Frame.GoBack();
            return;
        }

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        LoadingRing.IsActive = true;
        MatrixGrid.Children.Clear();
        MatrixGrid.RowDefinitions.Clear();
        MatrixGrid.ColumnDefinitions.Clear();

        try
        {
            var service = ((App)Application.Current).PermissionsService;
            _roles = await service.GetAllRolesAsync();
            
            // Filter "Administrador" out of columns? Usually Admin has all permissions implicit.
            // But user might want to see it. Code says "Admin always has all". 
            // So we can skip Admin column to avoid confusion or disable it.
            // Let's keep it but disabled.
            
            _permissions = await service.GetAllPermissionsAsync();

            // Load Matrix State
            _matrix.Clear();
            foreach (var role in _roles)
            {
                var perms = await service.GetPermissionsForRoleAsync(role.Id);
                _matrix[role.Id] = perms.Select(p => p.Id).ToHashSet();
            }

            RenderMatrix();
        }
        catch (Exception ex)
        {
            await new ContentDialog 
            { 
                Title = "Error", 
                Content = $"Error cargando permisos: {ex.Message}", 
                CloseButtonText = "Ok", 
                XamlRoot = this.XamlRoot 
            }.ShowAsync();
        }
        finally
        {
            LoadingRing.IsActive = false;
            _isLoading = false;
        }
    }

    private void RenderMatrix()
    {
        // 1. Define Columns
        // Col 0: Permission Name
        MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) }); // Widen first col

        int colIndex = 1;
        foreach (var role in _roles)
        {
            MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Widen role cols
            
            // Header
            var header = new TextBlock 
            { 
                Text = role.RoleName, 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(4),
                TextWrapping = TextWrapping.Wrap // Ensure text wraps if needed
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, colIndex);
            MatrixGrid.Children.Add(header);
            colIndex++;
        }

        MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header Row

        // 2. Define Rows (Permissions)
        // Group by Module (prefix)
        var groups = _permissions.GroupBy(p => p.PermissionKey.Split('.')[0]).OrderBy(g => g.Key);

        int rowIndex = 1;
        foreach (var group in groups)
        {
            // Group Header
            MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            var groupHeader = new TextBlock 
            { 
                Text = group.Key.ToUpper(), 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlForegroundBaseMediumBrush"],
                Margin = new Thickness(0, 12, 0, 4)
            };
            Grid.SetRow(groupHeader, rowIndex);
            Grid.SetColumn(groupHeader, 0);
            Grid.SetColumnSpan(groupHeader, _roles.Count + 1);
            MatrixGrid.Children.Add(groupHeader);
            rowIndex++;

            foreach (var perm in group)
            {
                MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Permission Label
                var label = new TextBlock 
                { 
                    Text = perm.Description ?? perm.PermissionKey, 
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 4, 4, 4)
                };
                
                // Tooltip
                ToolTipService.SetToolTip(label, perm.PermissionKey);

                Grid.SetRow(label, rowIndex);
                Grid.SetColumn(label, 0);
                MatrixGrid.Children.Add(label);

                // Checkboxes
                int rCol = 1;
                foreach (var role in _roles)
                {
                    // Container for centering
                    var container = new Grid();
                    
                    var cb = new CheckBox 
                    { 
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = new Tuple<Guid, Guid>(role.Id, perm.Id), // (RoleId, PermId)
                        Margin = new Thickness(0) // Reset margin
                    };

                    // Bind State
                    if (_matrix.ContainsKey(role.Id) && _matrix[role.Id].Contains(perm.Id))
                    {
                        cb.IsChecked = true;
                    }

                    // Admin Logic: Always Checked and Disabled
                    if (role.RoleName.Equals("Administrador", StringComparison.OrdinalIgnoreCase))
                    {
                        cb.IsChecked = true;
                        cb.IsEnabled = false;
                    }

                    cb.Checked += CheckBox_Changed;
                    cb.Unchecked += CheckBox_Changed;

                    container.Children.Add(cb);

                    Grid.SetRow(container, rowIndex);
                    Grid.SetColumn(container, rCol);
                    MatrixGrid.Children.Add(container);
                    rCol++;
                }

                rowIndex++;
            }
        }
    }

    private void CheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        if (sender is CheckBox cb && cb.Tag is Tuple<Guid, Guid> tag)
        {
            var roleId = tag.Item1;
            var permId = tag.Item2;

            if (!_matrix.ContainsKey(roleId)) _matrix[roleId] = new HashSet<Guid>();

            if (cb.IsChecked == true)
            {
                _matrix[roleId].Add(permId);
            }
            else
            {
                _matrix[roleId].Remove(permId);
            }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        LoadingRing.IsActive = true;
        SaveButton.IsEnabled = false;

        try
        {
            var service = ((App)Application.Current).PermissionsService;

            foreach (var role in _roles)
            {
                // Skip Admin as it's immutable in UI
                if (role.RoleName.Equals("Administrador", StringComparison.OrdinalIgnoreCase)) continue;

                if (_matrix.ContainsKey(role.Id))
                {
                    await service.UpdateRolePermissionsAsync(role.Id, _matrix[role.Id].ToList());
                }
            }

            await new ContentDialog 
            { 
                Title = "Guardado", 
                Content = "Los permisos han sido actualizados.", 
                CloseButtonText = "Ok", 
                XamlRoot = this.XamlRoot 
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            await new ContentDialog 
            { 
                Title = "Error", 
                Content = $"Error guardando: {ex.Message}", 
                CloseButtonText = "Close", 
                XamlRoot = this.XamlRoot 
            }.ShowAsync();
        }
        finally
        {
            LoadingRing.IsActive = false;
            SaveButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }
}
