using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.System;
using System.IO;
using System.Diagnostics;

namespace QMSFlowDoc.Client.Views;

public sealed partial class DocumentsView : Page
{
    public ObservableCollection<DocumentDto> Documents { get; } = new();
    public ObservableCollection<FolderDto> Folders { get; } = new();
    private FolderDto? _selectedFolder;
    private List<DocumentDto> _allDocuments = new(); // Cache for client-side filtering
    private string _currentSortColumn = "CreatedAt";
    private bool _sortAscending = false;

    public DocumentsView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadFoldersAsync();
        await LoadDocumentsAsync();
    }

    private async Task LoadFoldersAsync()
    {
        try
        {
            var app = (App)Application.Current;
            var folders = await app.FolderService.GetFoldersAsync();
            Folders.Clear();
            foreach (var f in folders) Folders.Add(f);
            FoldersList.ItemsSource = Folders;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading folders: {ex.Message}");
        }
    }

    private async Task LoadDocumentsAsync()
    {
        try
        {
            var app = (App)Application.Current;
            var includeObsolete = ShowObsoleteCheck?.IsChecked ?? false;
            var docs = await app.DocumentService.GetDocumentsAsync(includeObsolete);
            Documents.Clear();
            
            _allDocuments = docs.ToList();
            ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading documents: {ex.Message}");
        }
    }

    private void ApplyFilterAndSort()
    {
        var filtered = _selectedFolder == null 
            ? _allDocuments.Where(d => d.FolderId == null) 
            : _allDocuments.Where(d => d.FolderId == _selectedFolder.Id);

        var query = SearchBox.Text?.Trim().ToLower();
        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(d => 
                (d.Title != null && d.Title.ToLower().Contains(query)) ||
                (d.DocCode != null && d.DocCode.ToLower().Contains(query)));
        }

        // Sorting
        switch (_currentSortColumn)
        {
            case "DocCode": filtered = _sortAscending ? filtered.OrderBy(d => d.DocCode) : filtered.OrderByDescending(d => d.DocCode); break;
            case "Title": filtered = _sortAscending ? filtered.OrderBy(d => d.Title) : filtered.OrderByDescending(d => d.Title); break;
            case "TypeName": filtered = _sortAscending ? filtered.OrderBy(d => d.TypeName) : filtered.OrderByDescending(d => d.TypeName); break;
            case "Status": filtered = _sortAscending ? filtered.OrderBy(d => d.Status) : filtered.OrderByDescending(d => d.Status); break;
            case "CurrentVersionLabel": filtered = _sortAscending ? filtered.OrderBy(d => d.CurrentVersionLabel) : filtered.OrderByDescending(d => d.CurrentVersionLabel); break;
            case "NextReviewDue": filtered = _sortAscending ? filtered.OrderBy(d => d.NextReviewDue) : filtered.OrderByDescending(d => d.NextReviewDue); break;
            default: filtered = filtered.OrderByDescending(d => d.CreatedAt); break; // Default sort
        }

        Documents.Clear();
        foreach (var doc in filtered) Documents.Add(doc);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ApplyFilterAndSort();
    }

    private void Sort_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is string column)
        {
            if (_currentSortColumn == column) _sortAscending = !_sortAscending;
            else { _currentSortColumn = column; _sortAscending = true; }
            ApplyFilterAndSort();
        }
    }

    private async void FoldersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedFolder = FoldersList.SelectedItem as FolderDto;
        await LoadDocumentsAsync();
    }

    private async void ShowObsoleteCheck_Changed(object sender, RoutedEventArgs e)
    {
        await LoadDocumentsAsync();
    }

    // Explicitly prevent grid double-tap from opening documents
    private void Grid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true; // Prevent any default behavior
    }

    private async void OpenDocument(DocumentDto doc)
    {
         var app = (App)Application.Current;
         try 
         {
             // 1. Get Metadata for Filename
             var fullDoc = await app.DocumentService.GetDocumentByIdAsync(doc.Id);
             if (fullDoc == null || fullDoc.Versions == null || !fullDoc.Versions.Any())
             {
                 await new ContentDialog { Title="Error", Content="No se encontró el archivo.", CloseButtonText="OK", XamlRoot=this.XamlRoot}.ShowAsync();
                 return;
             }

             var version = fullDoc.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
             var fileName = version?.FileName ?? $"{doc.DocCode}.pdf";

             // 2. Get Content
             var bytes = await app.DocumentService.GetFileContentAsync(doc.Id);
             if (bytes == null || bytes.Length == 0)
             {
                 await new ContentDialog { Title="Error", Content="No se pudo descargar el contenido.", CloseButtonText="OK", XamlRoot=this.XamlRoot}.ShowAsync();
                 return;
             }

             // 3. ISO Watermark (CONTROLADO for screen view)
             var watermarkedBytes = await app.PdfWatermarkService.PrepareForScreenViewAsync(
                 bytes, 
                 fullDoc.DocCode, 
                 version?.VersionLabel ?? "v1.0", 
                 fullDoc.Status.ToString(), 
                 fullDoc.NextReviewDue);

             // 4. Save Temp
             var tempDir = Path.GetTempPath();
             var filePath = Path.Combine(tempDir, "VIEW_" + fileName);
             await File.WriteAllBytesAsync(filePath, watermarkedBytes);

             // 5. Launch
             Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
         }
         catch (Exception ex)
         {
             var details = $"Msg: {ex.Message}\nStack: {ex.StackTrace}";
             await new ContentDialog { Title="Error al abrir", Content=details, CloseButtonText="OK", XamlRoot=this.XamlRoot}.ShowAsync();
         }
    }

    private async void OpenDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is DocumentDto doc)
        {
             OpenDocument(doc);
        }
    }

    private async void PrintDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is DocumentDto doc)
        {
            var app = (App)Application.Current;
            try 
            {
                var fullDoc = await app.DocumentService.GetDocumentByIdAsync(doc.Id);
                if (fullDoc == null) return;

                var bytes = await app.DocumentService.GetFileContentAsync(doc.Id);
                if (bytes == null) return;

                var version = fullDoc.Versions?.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
                
                // Preparar para impresión (NO CONTROLADO)
                // Usamos metadata del footer de pantalla pero marca de agua de impresión
                var watermarkedBytes = await app.PdfWatermarkService.PrepareForExportAsync(
                    bytes, 
                    version?.VersionLabel ?? "v1.0", 
                    DateTime.Now);

                var tempDir = Path.GetTempPath();
                var filePath = Path.Combine(tempDir, $"PRINT_{doc.DocCode}_{Guid.NewGuid().ToString("N")[..8]}.pdf");
                await File.WriteAllBytesAsync(filePath, watermarkedBytes);

                // Abrir para que el usuario imprima
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                
                await app.DocumentService.LogAsync("PRINT_GEN", "Document", doc.Id, "Copia No Controlada generada desde botón de impresión");
            }
            catch (Exception ex)
            {
                await new ContentDialog { Title="Error al imprimir", Content=ex.Message, CloseButtonText="OK", XamlRoot=this.XamlRoot}.ShowAsync();
            }
        }
    }

    private async void EditSelected_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentsList.SelectedItem is DocumentDto doc)
        {
            if (!await CheckAdminPermission()) return;
            Frame.Navigate(typeof(DocumentEditorView), doc.Id);
        }
        else
        {
             // Show toast or help?
        }
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
         if (DocumentsList.SelectedItem is DocumentDto doc)
        {
            if (!await CheckAdminPermission()) return;
            await ConfirmAndDelete(doc);
        }
    }


    private void AddDocument_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(DocumentEditorView));
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        await ShowFolderDialog(null);
    }

    private async Task ShowFolderDialog(FolderDto? folderToEdit)
    {
        var isEdit = folderToEdit != null;
        var input = new TextBox { 
            Header = "Nombre de la carpeta", 
            PlaceholderText = "Ej: PNTs, Registros...",
            Text = folderToEdit?.Name ?? "",
            Margin = new Thickness(0, 16, 0, 0)
        };
        
        var dialog = new ContentDialog
        {
            Title = isEdit ? "Renombrar Carpeta" : "Crear Nueva Carpeta",
            Content = input,
            PrimaryButtonText = isEdit ? "Guardar" : "Crear",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
        {
            var app = (App)Application.Current;
            bool success;
            if (isEdit)
            {
                 // Check Admin Permission
                if (!await CheckAdminPermission()) return;
                success = await app.FolderService.RenameFolderAsync(folderToEdit!.Id, input.Text);
            }
            else
            {
                success = await app.FolderService.CreateFolderAsync(input.Text, null);
            }

            if (success) await LoadFoldersAsync();
        }
    }

    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = (sender as MenuFlyoutItem)?.DataContext as FolderDto ?? FoldersList.SelectedItem as FolderDto;
        if (folder != null)
        {
            await ShowFolderDialog(folder);
        }
    }

    private async void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = (sender as MenuFlyoutItem)?.DataContext as FolderDto ?? FoldersList.SelectedItem as FolderDto;
        if (folder == null) return;

        if (folder.DocumentCount > 0 || folder.SubFolderCount > 0)
        {
            var errorDialog = new ContentDialog { Title = "Error", Content = "No se puede eliminar una carpeta que contiene documentos o subcarpetas.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
            await errorDialog.ShowAsync();
            return;
        }

        if (!await CheckAdminPermission()) return;

        var confirm = new ContentDialog { Title = "Confirmar eliminación", Content = $"¿Seguro que deseas eliminar la carpeta '{folder.Name}'?", PrimaryButtonText = "Eliminar", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close, XamlRoot = this.XamlRoot };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            var app = (App)Application.Current;
            if (await app.FolderService.DeleteFolderAsync(folder.Id))
            {
                await LoadFoldersAsync();
                _selectedFolder = null; // Reset selection
                await LoadDocumentsAsync();
            }
        }
    }

    private async void EditDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is DocumentDto doc)
        {
            if (!await CheckAdminPermission()) return;
            Frame.Navigate(typeof(DocumentEditorView), doc.Id); // Pass ID to edit
        }
    }

    private async void DeleteDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is DocumentDto doc)
        {
            // DEBUG: Confirm handler is called
            var debug = new ContentDialog
            {
                Title = "Debug: Delete Iniciado",
                Content = $"Intentando eliminar: {doc.DocCode}\nID: {doc.Id}",
                CloseButtonText = "Continuar",
                XamlRoot = this.XamlRoot
            };
            await debug.ShowAsync();
            
            if (!await CheckAdminPermission()) return;
            await ConfirmAndDelete(doc);
        }
    }

    private async Task ConfirmAndDelete(DocumentDto doc)
    {
        var confirm = new ContentDialog 
        { 
            Title = "Confirmar eliminación", 
            Content = $"¿Seguro que deseas eliminar el documento '{doc.DocCode}'?\nEl archivo se moverá a la papelera (_Trash).", 
            PrimaryButtonText = "Eliminar", 
            CloseButtonText = "Cancelar", 
            DefaultButton = ContentDialogButton.Close, 
            XamlRoot = this.XamlRoot 
        };
        
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                var app = (App)Application.Current;
                var result = await app.DocumentService.DeleteDocumentAsync(doc.Id);
                
                if (result)
                {
                    await LoadDocumentsAsync();
                    
                    // Success message
                    var success = new ContentDialog
                    {
                        Title = "Éxito",
                        Content = $"Documento '{doc.DocCode}' eliminado correctamente.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await success.ShowAsync();
                }
                else
                {
                    // Failure - show error
                    var error = new ContentDialog
                    {
                        Title = "Error al eliminar",
                        Content = $"No se pudo eliminar el documento '{doc.DocCode}'.\n\nPosibles causas:\n- El documento no existe en la base de datos\n- Error al eliminar archivos físicos\n- Error de conexión\n\nPor favor, verifica los logs del sistema.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await error.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                // Exception - show detailed error
                var error = new ContentDialog
                {
                    Title = "Excepción al eliminar",
                    Content = $"Error: {ex.Message}\n\nStack: {(ex.StackTrace ?? "No stack trace").Substring(0, Math.Min(200, (ex.StackTrace ?? "").Length))}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await error.ShowAsync();
            }
        }
    }


    // OpenDocument_Click replaced above

    private async Task<bool> CheckAdminPermission()
    {
        var input = new PasswordBox { Header = "Contraseña de Administrador", Margin = new Thickness(0, 16, 0, 0) };
        var dialog = new ContentDialog
        {
            Title = "Permiso de Administrador Requerido",
            Content = input,
            PrimaryButtonText = "Verificar",
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            // Hardcoded "admin" for now, ideally check against backend or config
            if (input.Password == "admin123") return true; // Fixed password per user feedback
            
            var err = new ContentDialog { Title = "Error", Content = "Contraseña incorrecta.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
            await err.ShowAsync();
        }
        return false;
    }

    private void DocumentsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DocumentDto doc)
        {
            // Frame.Navigate(typeof(DocumentDetailsView), doc.Id);
        }
    }
}
