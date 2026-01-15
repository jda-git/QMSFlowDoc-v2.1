using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class DocumentEditorView : Page
{
    private byte[]? _fileData;
    private string? _fileName;
    private string? _contentType;
    private Guid? _editingDocumentId = null;

    public DocumentEditorView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadFoldersAsync();
        await LoadDocumentTypesAsync();
        StatusCombo.ItemsSource = Enum.GetValues(typeof(DocumentStatus));

        if (e.Parameter is Guid docId)
        {
            _editingDocumentId = docId;
            await LoadDocumentDetailsAsync(docId);
        }
    }

    private async Task LoadDocumentTypesAsync()
    {
        try
        {
            var types = await ((App)Application.Current).DocumentService.GetDocumentTypesAsync();
             TypeCombo.ItemsSource = types;
             if (types.Any()) TypeCombo.SelectedIndex = 0;
        }
        catch { }
    }

    private async Task LoadDocumentDetailsAsync(Guid docId)
    {
        try
        {
            var doc = await ((App)Application.Current).DocumentService.GetDocumentByIdAsync(docId);
            if (doc != null)
            {
                TitleBox.Text = doc.Title;
                CodeBox.Text = doc.DocCode;
                AreaBox.Text = doc.Area ?? "";
                ProcessBox.Text = doc.Process ?? "";
                ReviewIntervalBox.Value = doc.ReviewIntervalMonths ?? 12;

                // Select Folder
                if (doc.FolderId.HasValue)
                {
                     var folders = FolderCombo.ItemsSource as System.Collections.Generic.IEnumerable<FolderDto>;
                     if (folders != null) FolderCombo.SelectedItem = folders.FirstOrDefault(f => f.Id == doc.FolderId.Value);
                }

                // Select Type
                 var types = TypeCombo.ItemsSource as System.Collections.Generic.IEnumerable<QMSFlowDoc.Shared.Models.DocumentType>;
                 if (types != null && doc.DocumentType != null) TypeCombo.SelectedItem = types.FirstOrDefault(t => t.Id == doc.DocumentTypeId);

                 // Version (if exists)
                 if (doc.Versions != null && doc.Versions.Any())
                 {
                     VersionBox.Text = doc.Versions.OrderByDescending(v => v.CreatedAt).First().VersionLabel;
                 }

                 StatusCombo.SelectedItem = doc.Status;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error cargando: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadFoldersAsync()
    {
        try
        {
            var folders = await ((App)Application.Current).FolderService.GetFoldersAsync();
            if (folders.Any())
            {
                FolderCombo.ItemsSource = folders;
                FolderCombo.SelectedIndex = 0;
            }
            else
            {
                FolderCombo.ItemsSource = null;
                FolderCombo.PlaceholderText = "No hay carpetas disponibles";
            }
        }
        catch { /* Handle error */ }
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        
        // Initialize picker with window handle (Required for WinUI 3)
        var window = ((App)Application.Current).Window;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".pdf");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _fileName = file.Name;
            _contentType = file.ContentType;
            FileNameText.Text = _fileName;

            using (var stream = await file.OpenReadAsync())
            {
                _fileData = new byte[stream.Size];
                using (var reader = new Windows.Storage.Streams.DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(_fileData);
                }
            }
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(CodeBox.Text))
        {
            ErrorText.Text = "Título y Código son obligatorios.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var folderId = (FolderCombo.SelectedItem as FolderDto)?.Id;
            var typeId = (TypeCombo.SelectedItem as QMSFlowDoc.Shared.Models.DocumentType)?.Id;
            var app = (App)Application.Current;
            bool success;

            // Check if creating a NEW document WITH a file - use local mode
            if (!_editingDocumentId.HasValue && _fileData != null && _fileName != null)
            {
                var status = (DocumentStatus?)StatusCombo.SelectedItem ?? DocumentStatus.DRAFT;
                
                try
                {
                    // Get folder name for correct path
                    var folderName = (FolderCombo.SelectedItem as FolderDto)?.Name;

                    var newDoc = await app.DocumentService.CreateDocumentWithFileAsync(
                        CodeBox.Text,
                        TitleBox.Text,
                        status,
                        typeId,                                             
                        (int)ReviewIntervalBox.Value,                       
                        string.IsNullOrWhiteSpace(VersionBox.Text) ? "1.0" : VersionBox.Text, 
                        AreaBox.Text,                                       // Pass Area
                        ProcessBox.Text,                                    // Pass Process
                        _fileData,
                        _fileName,
                        folderName ?? ""); // Pass folder name
                    
                    success = newDoc != null;
                    if (success)
                    {
                        // Success - file was copied to local/network folders
                        Frame.GoBack();
                        return;
                    }
                }
                catch (NotImplementedException)
                {
                    // Fall through to API mode below
                }
            }

            // Original API mode (for editing or when file is not provided)
            var request = new CreateDocumentRequest(
                CodeBox.Text,
                TitleBox.Text,
                typeId,
                folderId,
                AreaBox.Text,
                ProcessBox.Text,
                (int)ReviewIntervalBox.Value,
                string.IsNullOrWhiteSpace(VersionBox.Text) ? "v1.0" : VersionBox.Text,
                StatusCombo.SelectedItem as DocumentStatus?
            );

            if (_editingDocumentId.HasValue)
            {
                success = await app.DocumentService.UpdateDocumentAsync(_editingDocumentId.Value, request);
            }
            else
            {
                var newDoc = await app.DocumentService.CreateDocumentAsync(request);
                success = newDoc != null;
                if (success) _editingDocumentId = newDoc!.Id; // Set ID for potential file upload
            }
            
            if (success)
            {
                // If a new file was selected, upload it
                if (_fileData != null && _fileName != null && _contentType != null)
                {
                    // Validation again before upload
                    if (!Path.GetExtension(_fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        var err = new ContentDialog { Title = "Error de archivo", Content = "Solo se permiten archivos .PDF.", CloseButtonText = "OK", XamlRoot = this.XamlRoot };
                        await err.ShowAsync();
                        return;
                    }

                    var uploadSuccess = await app.DocumentService.UploadFileAsync(_editingDocumentId!.Value, _fileData, _fileName, _contentType);
                    if (!uploadSuccess)
                    {
                        ErrorText.Text = "Datos guardados, pero falló la carga del archivo.";
                        ErrorText.Visibility = Visibility.Visible;
                        return;
                    }
                }

                Frame.GoBack();
            }
            else
            {
                ErrorText.Text = "Error al guardar. Verifique si el código ya existe.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (ex.InnerException != null) message += $" -> {ex.InnerException.Message}";
            
            ErrorText.Text = $"Error: {message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
