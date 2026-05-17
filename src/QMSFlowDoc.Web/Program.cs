using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Data;
using QMSFlowDoc.DocumentStorage;
using QMSFlowDoc.Web.Rendering;
using QMSFlowDoc.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var urls = builder.Configuration.GetSection("Server:Urls").Get<string[]>() ?? ["http://0.0.0.0:5080"];
builder.WebHost.UseUrls(urls);

var connectionString = builder.Configuration.GetConnectionString("QMSFlowDoc")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:QMSFlowDoc configuration value.");

builder.Services.AddDbContext<QmsFlowDocDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql => sql.CommandTimeout(120));
});

builder.Services.AddSingleton<IDocumentStorageService>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var rootPath = configuration["DocumentStorage:RootPath"]
        ?? throw new InvalidOperationException("Missing DocumentStorage:RootPath configuration value.");
    var logger = sp.GetRequiredService<ILogger<CentralDocumentStorageService>>();
    return new CentralDocumentStorageService(rootPath, logger);
});

builder.Services.AddScoped<IWebAuthService, WebAuthService>();
builder.Services.AddScoped<IDocumentPortalService, DocumentPortalService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? "QMSFlowDocV3.Auth";
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(builder.Configuration.GetValue("Authentication:ExpireHours", 10));
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

await EnsureDocumentRepositoryAsync(app.Services);

app.MapGet("/login", () => Results.Content(HtmlPage.Login(), "text/html"))
    .AllowAnonymous();

app.MapPost("/login", async (HttpContext http, IWebAuthService auth, CancellationToken ct) =>
{
    var form = await http.Request.ReadFormAsync(ct);
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var user = await auth.ValidateCredentialsAsync(username, password, ct);
    if (user is null)
        return Results.Content(HtmlPage.Login("Usuario o contraseña no válidos, o cuenta bloqueada."), "text/html");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new("FullName", user.FullName)
    };
    claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapGet("/", async (HttpContext http, IDocumentPortalService portal, CancellationToken ct) =>
{
    var summary = await portal.GetDashboardSummaryAsync(ct);
    var displayName = http.User.FindFirstValue("FullName") ?? http.User.Identity?.Name ?? "Usuario";
    return Results.Content(HtmlPage.Dashboard(summary, displayName), "text/html");
}).RequireAuthorization();

app.MapGet("/documents", async (IDocumentPortalService portal, CancellationToken ct) =>
{
    var documents = await portal.GetDocumentsAsync(ct);
    return Results.Content(HtmlPage.Documents(documents), "text/html");
}).RequireAuthorization();

app.MapPost("/documents", async (HttpContext http, IDocumentPortalService portal, CancellationToken ct) =>
{
    var form = await http.Request.ReadFormAsync(ct);
    var file = form.Files["file"];
    if (file is null)
        return Results.Content(HtmlPage.Error("Archivo obligatorio", "Selecciona un archivo para subir al repositorio central."), "text/html");

    var userIdText = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var userId = Guid.TryParse(userIdText, out var parsedUserId) ? parsedUserId : (Guid?)null;

    await portal.CreateDraftDocumentAsync(
        form["docCode"].ToString(),
        form["title"].ToString(),
        form["area"].ToString(),
        form["process"].ToString(),
        file,
        userId,
        ct);

    var documents = await portal.GetDocumentsAsync(ct);
    return Results.Content(HtmlPage.Documents(documents, "Documento subido correctamente al servidor."), "text/html");
}).RequireAuthorization();

app.MapGet("/documents/{id:guid}/download", async (Guid id, IDocumentPortalService portal, CancellationToken ct) =>
{
    var file = await portal.OpenCurrentFileAsync(id, ct);
    if (file is null)
        return Results.NotFound("No hay archivo vigente para este documento.");

    return Results.File(file.Stream, file.MimeType, file.FileName);
}).RequireAuthorization();

app.MapGet("/health", (IConfiguration configuration) => Results.Json(new
{
    status = "OK",
    application = "QMSFlowDoc v3 Web",
    urls,
    documentStorage = configuration["DocumentStorage:RootPath"],
    serverTimeUtc = DateTime.UtcNow
})).RequireAuthorization();

app.Run();

static async Task EnsureDocumentRepositoryAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var storage = scope.ServiceProvider.GetRequiredService<IDocumentStorageService>();
    await storage.CreateFolderStructureAsync();
}
