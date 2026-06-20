using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();
builder.Services.AddScoped<PropertyPayPro.Services.BillingService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

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
});

app.Run();
