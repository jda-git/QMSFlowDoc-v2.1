using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Client.Services.Sync;
using System.Collections.Generic;
using System.Linq;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class SyncConfirmationDialog : ContentDialog
{
    private readonly List<SyncChange> _changes;

    public List<SyncChange> ApprovedChanges => _changes;

    public SyncConfirmationDialog(List<SyncChange> changes)
    {
        this.InitializeComponent();
        _changes = changes;
        LoadChanges();
    }

    private void LoadChanges()
    {
        var downloads = _changes.Where(c => c.Direction == SyncDirection.Download && !c.IsConflict).ToList();
        var uploads = _changes.Where(c => c.Direction == SyncDirection.Upload && !c.IsConflict).ToList();
        var conflicts = _changes.Where(c => c.IsConflict).ToList();

        // Downloads
        if (downloads.Any())
        {
            DownloadsSection.Visibility = Visibility.Visible;
            DownloadsList.ItemsSource = downloads;
        }

        // Uploads
        if (uploads.Any())
        {
            UploadsSection.Visibility = Visibility.Visible;
            UploadsList.ItemsSource = uploads;
        }

        // Conflicts
        if (conflicts.Any())
        {
            ConflictsSection.Visibility = Visibility.Visible;
            ConflictsList.ItemsSource = conflicts;
        }

        // Summary
        var summaryParts = new List<string>();
        if (downloads.Any()) summaryParts.Add($"{downloads.Count} archivo(s) a descargar");
        if (uploads.Any()) summaryParts.Add($"{uploads.Count} archivo(s) a subir");
        if (conflicts.Any()) summaryParts.Add($"{conflicts.Count} conflicto(s) a resolver");

        SummaryText.Text = summaryParts.Any() 
            ? $"Resumen: {string.Join(", ", summaryParts)}."
            : "No hay cambios pendientes.";

        // Disable primary button if there are unresolved conflicts
        UpdatePrimaryButtonState();
    }

    private void UseLocal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SyncChange change)
        {
            change.ConflictResolution = SyncDirection.Upload;
            change.IsConflict = false; // Mark as resolved
            UpdateConflictDisplay(change, "Local");
        }
    }

    private void UseNetwork_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SyncChange change)
        {
            change.ConflictResolution = SyncDirection.Download;
            change.IsConflict = false; // Mark as resolved
            UpdateConflictDisplay(change, "Red");
        }
    }

    private void UpdateConflictDisplay(SyncChange change, string choice)
    {
        // Refresh the conflicts list by reassigning
        var conflicts = _changes.Where(c => c.Direction == SyncDirection.Conflict).ToList();
        var unresolvedCount = conflicts.Count(c => c.IsConflict);
        
        // Update summary
        var downloads = _changes.Where(c => c.Direction == SyncDirection.Download && !c.IsConflict).ToList();
        var uploads = _changes.Where(c => c.Direction == SyncDirection.Upload && !c.IsConflict).ToList();
        var resolved = conflicts.Count(c => !c.IsConflict);
        
        var summaryParts = new List<string>();
        if (downloads.Any()) summaryParts.Add($"{downloads.Count} a descargar");
        if (uploads.Any()) summaryParts.Add($"{uploads.Count} a subir");
        if (unresolvedCount > 0) summaryParts.Add($"{unresolvedCount} conflicto(s) sin resolver");
        if (resolved > 0) summaryParts.Add($"{resolved} conflicto(s) resuelto(s)");

        SummaryText.Text = $"Resumen: {string.Join(", ", summaryParts)}.";
        
        UpdatePrimaryButtonState();
    }

    private void UpdatePrimaryButtonState()
    {
        // Enable sync button only if all conflicts are resolved
        var hasUnresolvedConflicts = _changes.Any(c => c.Direction == SyncDirection.Conflict && c.IsConflict);
        IsPrimaryButtonEnabled = !hasUnresolvedConflicts;
    }
}
