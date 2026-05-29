using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<QmsDbContext>(options =>
    options.UseSqlite(connectionString, b => b.MigrationsAssembly("QMSFlowDoc.Infrastructure")));

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<QmsDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, QMSFlowDoc.Web.Security.MigrationPasswordHasher>();

// Document Storage and Document Management Services
var documentStorageConfig = builder.Configuration.GetSection("DocumentStorage");
var rootPath = documentStorageConfig["RootPath"] ?? throw new InvalidOperationException("DocumentStorage:RootPath not configured.");

builder.Services.AddSingleton<QMSFlowDoc.DocumentStorage.IDocumentStorageService>(sp =>
    new QMSFlowDoc.DocumentStorage.CentralDocumentStorageService(rootPath, sp.GetRequiredService<ILogger<QMSFlowDoc.DocumentStorage.CentralDocumentStorageService>>()));

builder.Services.AddSingleton<QMSFlowDoc.Application.Services.Documents.IPdfWatermarkService, QMSFlowDoc.Infrastructure.Services.Documents.PdfWatermarkService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Folders.IFolderService, QMSFlowDoc.Infrastructure.Services.Folders.FolderService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Documents.IDocumentService, QMSFlowDoc.Infrastructure.Services.Documents.DocumentService>();
builder.Services.AddScoped<QMSFlowDoc.Application.Services.Inventory.IInventoryService, QMSFlowDoc.Infrastructure.Services.Inventory.InventoryService>();

builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/login";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllersWithViews();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await QMSFlowDoc.Infrastructure.Seed.DbInitializer.SeedIdentityAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

await app.RunAsync();
