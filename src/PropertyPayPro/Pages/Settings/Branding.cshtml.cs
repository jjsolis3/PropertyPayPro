using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class BrandingModel : PageModel
{
    private readonly AppSettingsService _settings;
    private readonly IDocumentStorage _storage;
    private readonly ApplicationDbContext _db;
    private readonly IAuditLog _audit;

    public BrandingModel(AppSettingsService settings, IDocumentStorage storage,
        ApplicationDbContext db, IAuditLog audit)
    {
        _settings = settings;
        _storage = storage;
        _db = db;
        _audit = audit;
    }

    [BindProperty]
    public AppSettings Input { get; set; } = new();

    public AppSettings Current { get; private set; } = new();

    public IReadOnlyList<(string Id, string Label)> CommonZones => AppTimeZone.Common;
    public IEnumerable<string> OtherZones =>
        AppTimeZone.AllIds().Except(AppTimeZone.Common.Select(z => z.Id));

    public async Task<IActionResult> OnGetAsync()
    {
        Current = await _settings.GetAsync();
        Input = new AppSettings
        {
            AppName = Current.AppName,
            PrimaryColor = Current.PrimaryColor,
            AccentColor = Current.AccentColor,
            LogoStorageKey = Current.LogoStorageKey,
            LogoSmallStorageKey = Current.LogoSmallStorageKey,
            PublicBaseUrl = Current.PublicBaseUrl,
            TimeZoneId = Current.TimeZoneId,
            FromEmailOverride = Current.FromEmailOverride,
            FromNameOverride = Current.FromNameOverride,
            DefaultRentDueDay = Current.DefaultRentDueDay,
            DefaultLateFeeGraceDays = Current.DefaultLateFeeGraceDays,
            DefaultLateFeeAmount = Current.DefaultLateFeeAmount
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? logoMain, IFormFile? logoSmall)
    {
        Current = await _settings.GetAsync();

        Input.PublicBaseUrl = string.IsNullOrWhiteSpace(Input.PublicBaseUrl)
            ? null
            : Input.PublicBaseUrl.Trim().TrimEnd('/');
        if (Input.PublicBaseUrl is not null
            && !(Uri.TryCreate(Input.PublicBaseUrl, UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
        {
            ModelState.AddModelError("Input.PublicBaseUrl", "Enter a full URL starting with https://");
        }
        if (!AppTimeZone.IsValid(Input.TimeZoneId))
        {
            ModelState.AddModelError("Input.TimeZoneId", "Pick a time zone from the list.");
        }

        if (!ModelState.IsValid) return Page();
        var previousZone = Current.TimeZoneId;

        // Update only the fields this page owns, so saving Branding never
        // resets settings that live on other pages.
        Current.AppName = Input.AppName;
        Current.PrimaryColor = Input.PrimaryColor;
        Current.AccentColor = Input.AccentColor;
        Current.PublicBaseUrl = Input.PublicBaseUrl;
        Current.TimeZoneId = Input.TimeZoneId;
        if (logoMain is { Length: > 0 })
        {
            Current.LogoStorageKey = await SaveLogoAsync(logoMain, "main");
        }
        if (logoSmall is { Length: > 0 })
        {
            Current.LogoSmallStorageKey = await SaveLogoAsync(logoSmall, "small");
        }

        await _settings.SaveAsync(Current);

        if (Input.TimeZoneId != previousZone)
        {
            AppTimeZone.Apply(Input.TimeZoneId);
            // Re-time queued daily sweeps so they fire at the run hour in the new zone.
            var nextRun = Services.ScheduledTasks.RecurringTaskHelpers.NextDailyRunUtc(DateTime.UtcNow);
            var kinds = Services.ScheduledTasks.RecurringTaskHelpers.RecurringKinds;
            await _db.ScheduledTasks
                .Where(t => kinds.Contains(t.Kind) && t.Status == ScheduledTaskStatus.Pending && t.RunAtUtc > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RunAtUtc, nextRun));
            await _audit.WriteAsync("TimeZoneChanged", nameof(AppSettings), AppSettingsService.SingletonId,
                $"{previousZone} -> {Input.TimeZoneId}");
        }

        TempData["Message"] = "Branding saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearLogoAsync(string which)
    {
        Current = await _settings.GetAsync();
        var updated = Current;
        if (which == "main")
        {
            if (!string.IsNullOrEmpty(updated.LogoStorageKey))
                try { await _storage.DeleteAsync(updated.LogoStorageKey); } catch { }
            updated.LogoStorageKey = null;
        }
        else if (which == "small")
        {
            if (!string.IsNullOrEmpty(updated.LogoSmallStorageKey))
                try { await _storage.DeleteAsync(updated.LogoSmallStorageKey); } catch { }
            updated.LogoSmallStorageKey = null;
        }
        await _settings.SaveAsync(updated);
        TempData["Message"] = "Logo cleared (default restored).";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetLogoAsync(string which)
    {
        Current = await _settings.GetAsync();
        var key = which == "small" ? Current.LogoSmallStorageKey : Current.LogoStorageKey;
        if (string.IsNullOrEmpty(key)) return NotFound();
        var stream = await _storage.OpenReadAsync(key);
        return File(stream, "image/png");
    }

    private async Task<string> SaveLogoAsync(IFormFile file, string label)
    {
        var safeExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (safeExt is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new InvalidOperationException("Logo must be PNG, JPG, or WebP.");
        await using var stream = file.OpenReadStream();
        return await _storage.SaveAsync("branding", $"logo-{label}{safeExt}", stream);
    }
}
