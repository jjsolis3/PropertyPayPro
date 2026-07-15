using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

var usCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = usCulture;
CultureInfo.DefaultThreadCurrentUICulture = usCulture;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Persist Data Protection keys to the DB so reset-password tokens, invite
// links, and sign-in cookies survive container restarts. Without this,
// every redeploy invalidates all outstanding tokens.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("PropertyPayPro");

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    // Admin surfaces require the Admin role. Applied via the AuthorizeFolder
    // conventions below so we don't have to sprinkle [Authorize(Roles=…)] on
    // every page. Tenants get a distinct policy and their own /Portal folder.
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireRole(IdentitySeed.AdminRole));

    options.AddPolicy("TenantOnly", policy =>
        policy.RequireAuthenticatedUser().RequireRole(IdentitySeed.TenantRole));
});

builder.Services
    .AddRazorPages(options =>
    {
        // Login and the password reset flow stay open. Everything else (including
        // the dashboard at "/") is gated by the fallback policy above.
        options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
        options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Logout");
        options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPassword");
        options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPassword");
        options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/AccessDenied");
        options.Conventions.AllowAnonymousToPage("/Error");

        // Admin-only folders. Each existing top-level Razor folder is gated on
        // the Admin role so a tenant-portal login can't wander into them.
        // Identity area pages (login/manage), the future /Portal folder, and
        // the root pages "/Index", "/Privacy" fall through to the default
        // authenticated-user policy.
        var adminFolders = new[]
        {
            "/Properties", "/Tenants", "/Leases", "/Bills", "/Payments",
            "/Receipts", "/Expenses", "/ServiceTickets", "/Documents",
            "/Reports", "/Settings", "/Users"
        };
        foreach (var folder in adminFolders)
        {
            options.Conventions.AuthorizeFolder(folder, "AdminOnly");
        }
        // Root-level admin pages that aren't under a folder.
        options.Conventions.AuthorizePage("/Index", "AdminOnly");
        options.Conventions.AuthorizePage("/Privacy", "AdminOnly");

        options.Conventions.AuthorizeFolder("/Portal", "TenantOnly");
    });

builder.Services.AddMemoryCache();
builder.Services.AddScoped<PropertyPayPro.Services.AppSettingsService>();
builder.Services.AddScoped<PropertyPayPro.Services.BillingService>();
builder.Services.AddSingleton<PropertyPayPro.Services.IDocumentStorage, PropertyPayPro.Services.LocalFileSystemDocumentStorage>();
builder.Services.AddHealthChecks();

builder.Services.Configure<PropertyPayPro.Services.EmailOptions>(options =>
{
    options.Host = builder.Configuration["SMTP_HOST"] ?? "";
    if (int.TryParse(builder.Configuration["SMTP_PORT"], out var port)) options.Port = port;
    options.User = builder.Configuration["SMTP_USER"] ?? "";
    options.Password = builder.Configuration["SMTP_PASSWORD"] ?? "";
    options.FromAddress = builder.Configuration["SMTP_FROM"] ?? options.User;
    options.FromName = builder.Configuration["SMTP_FROM_NAME"] ?? "PropertyPayPro";
    if (bool.TryParse(builder.Configuration["SMTP_USE_STARTTLS"], out var tls)) options.UseStartTls = tls;
});
builder.Services.AddSingleton<PropertyPayPro.Services.IEmailSender, PropertyPayPro.Services.SmtpEmailSender>();
builder.Services.AddScoped<PropertyPayPro.Services.MailService>();
builder.Services.AddScoped<PropertyPayPro.Services.PdfService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await PropertyPayPro.Data.IdentitySeed.EnsureAdminAsync(scope.ServiceProvider, app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(usCulture),
    SupportedCultures = new[] { usCulture },
    SupportedUICultures = new[] { usCulture }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHealthChecks("/health")
   .AllowAnonymous();

// Public branded-logo endpoint (used by login page + nav). Allows anonymous so the
// login page can render before the user is authenticated.
app.MapGet("/branding/logo/{which}", async (
    string which,
    PropertyPayPro.Services.AppSettingsService settings,
    PropertyPayPro.Services.IDocumentStorage storage) =>
{
    var s = await settings.GetAsync();
    var key = which == "small" ? s.LogoSmallStorageKey : s.LogoStorageKey;
    if (string.IsNullOrEmpty(key)) return Results.NotFound();
    var stream = await storage.OpenReadAsync(key);
    return Results.File(stream, "image/png");
}).AllowAnonymous();

app.MapGet("/api/leases/{leaseId:int}/outstanding-charges", async (
    int leaseId,
    ApplicationDbContext db,
    Microsoft.AspNetCore.Authorization.IAuthorizationService authz,
    HttpContext ctx) =>
{
    if (!(ctx.User.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();

    var charges = await db.RentalCharges
        .Where(c => c.LeaseId == leaseId)
        .Include(c => c.Allocations)
        .OrderBy(c => c.BillingPeriodStart)
        .ThenBy(c => c.Kind)
        .ToListAsync();

    var items = charges
        .Where(c => c.Balance > 0)
        .Select(c => new
        {
            id = c.Id,
            label = c.PeriodLabel,
            dueDate = c.DueDate.ToString("yyyy-MM-dd"),
            balance = c.Balance
        });

    return Results.Ok(items);
});

app.MapPost("/api/jobs/generate-monthly-charges", async (
    HttpContext ctx,
    PropertyPayPro.Services.BillingService billing,
    IConfiguration config,
    int? year,
    int? month) =>
{
    var expectedToken = config["JOB_TOKEN"];
    if (string.IsNullOrWhiteSpace(expectedToken))
    {
        return Results.Problem("JOB_TOKEN is not configured.", statusCode: 500);
    }

    var providedToken = ctx.Request.Headers["X-Job-Token"].ToString();
    if (providedToken != expectedToken)
    {
        return Results.Unauthorized();
    }

    var now = DateTime.UtcNow;
    var y = year ?? now.Year;
    var m = month ?? now.Month;

    var created = await billing.GenerateChargesForPeriodAsync(y, m);
    return Results.Ok(new { year = y, month = m, created });
}).AllowAnonymous();

app.Run();
