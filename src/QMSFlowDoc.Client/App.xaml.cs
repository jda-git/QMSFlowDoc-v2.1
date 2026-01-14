using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Net.Http;

namespace QMSFlowDoc.Client;

public partial class App : Application
{
    public Window? Window { get; private set; }
    public Services.IAuthService AuthService { get; }
    public Services.ILocalCacheService LocalCacheService { get; } = new Services.LocalCacheService();
    public Services.IDocumentService DocumentService { get; }
    public Services.IInventoryService InventoryService { get; }
    public Services.IEquipmentService EquipmentService { get; }
    public Services.IStaffService StaffService { get; }
    public Services.IQualityService QualityService { get; }
    public Services.IImprovementService ImprovementService { get; }
    public Services.IDashboardService DashboardService { get; }
    public Services.ISearchService SearchService { get; }
    public Services.IFolderService FolderService { get; }
    public Services.IConfigurationService ConfigurationService { get; } 
    public Services.ITrainingService TrainingService { get; }
    public Services.ICompetencyService CompetencyService { get; }
    public Services.IAuthorizationService AuthorizationService { get; }
    public Services.IPrintingService PrintingService { get; } = new Services.PrintingService();
    
    // Sync Infrastructure
    public Services.Sync.SnapshotStore SnapshotStore { get; }
    public Services.Sync.GoogleDriveProvider DriveProvider { get; }
    public Services.Sync.SyncEngine DriveSyncEngine { get; }
    public Services.Sync.SyncLogger SyncLogger { get; }
    public Services.Sync.AuditLogger AuditLogger { get; }
    
    // Document Management Services
    public Services.Documents.PdfWatermarkService PdfWatermarkService { get; }
    public Services.Documents.DocumentRenderer DocumentRenderer { get; }
    public Services.Documents.PrintControlService PrintControlService { get; }
    
    // Configuration & Audit
    public Services.AuditLogger EquipmentAuditLogger { get; }
    public Services.NetworkConfigStore NetworkConfigStore { get; } = new Services.NetworkConfigStore();
    public Services.LocalConfigStore LocalConfigStore { get; } = new Services.LocalConfigStore();
    
    private System.Threading.Timer? _syncTimer;

    public App()
    {
        this.InitializeComponent();
        
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000/api/") };
        
        AuthService = new Services.AuthService(httpClient);
        DocumentService = new Services.DocumentService(httpClient, LocalCacheService);
        InventoryService = new Services.InventoryService(httpClient);
        EquipmentService = new Services.EquipmentService(httpClient);
        StaffService = new Services.StaffService(httpClient);
        QualityService = new Services.QualityService(httpClient);
        ImprovementService = new Services.ImprovementService(httpClient);
        DashboardService = new Services.DashboardService(httpClient);
        SearchService = new Services.SearchService(httpClient);
        FolderService = new Services.FolderService(httpClient);
        ConfigurationService = new Services.ConfigurationService(httpClient);
        TrainingService = new Services.TrainingService(httpClient);
        CompetencyService = new Services.CompetencyService(httpClient);
        AuthorizationService = new Services.AuthorizationService(httpClient);
        EquipmentAuditLogger = new Services.AuditLogger(ConfigurationService); 
        
        // Init Sync Infrastructure
        SnapshotStore = new Services.Sync.SnapshotStore();
        DriveProvider = new Services.Sync.GoogleDriveProvider();
        SyncLogger = new Services.Sync.SyncLogger();
        AuditLogger = new Services.Sync.AuditLogger();
        
        var localDocsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QMSFlowDoc", "Files");
        DriveSyncEngine = new Services.Sync.SyncEngine(SnapshotStore, DriveProvider, SyncLogger, AuditLogger, localDocsPath);
        
        // Init Document Management Services
        PdfWatermarkService = new Services.Documents.PdfWatermarkService();
        DocumentRenderer = new Services.Documents.DocumentRenderer(PdfWatermarkService, AuditLogger);
        PrintControlService = new Services.Documents.PrintControlService(PdfWatermarkService, AuditLogger);
    }
    
    public MainWindow? MainWindow => Window as MainWindow;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            this.UnhandledException += (s, e) =>
            {
                e.Handled = true;
                MessageBox(IntPtr.Zero, $"Unhandled Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Critical Error", 0x10);
            };

            await LocalCacheService.InitializeAsync();
            await SnapshotStore.InitializeAsync();
            await PrintControlService.InitializeAsync(); // Init PrintLog database
            
            // Initialize LocalDocumentStore if using local mode
            // Initialize LocalDocumentStore via Service (explicit call)
            await DocumentService.InitializeAsync();
            
            // Initialize folder structure if configured
            try
            {
                if (await NetworkConfigStore.ValidatePathsAsync())
                {
                    await NetworkConfigStore.InitializeStructureAsync();
                }
            }
            catch { /* Not configured or network down */ }
            
            // Load Drive Folder ID from local configuration (no authentication required)
            var driveFolderId = await LocalConfigStore.GetDriveFolderIdAsync();
            if (!string.IsNullOrWhiteSpace(driveFolderId))
            {
                DriveSyncEngine.DriveFolderId = driveFolderId;
            }
            
            // Start Sync Loop (every 5 mins) - will skip if DriveFolderId not configured
            _syncTimer = new System.Threading.Timer(async _ => await DriveSyncEngine.RunSyncAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));

            Window = new MainWindow();
            
            if (!AuthService.IsAuthenticated)
            {
                NavigateToLogin();
            }
            else
            {
                NavigateToMain();
            }

            Window.Activate();
        }
        catch (Exception ex)
        {
            // Catch startup errors that crash the app silently
            MessageBox(IntPtr.Zero, $"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Fatal Startup Error", 0x10);
        }
    }

    public void NavigateToLogin()
    {
        if (Window is MainWindow mw)
        {
            mw.ShowLogin();
        }
    }

    public void NavigateToMain()
    {
        if (Window is MainWindow mw)
        {
            mw.ShowMain();
        }
    }
}
