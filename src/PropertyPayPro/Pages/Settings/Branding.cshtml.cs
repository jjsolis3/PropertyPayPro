using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class BrandingModel : PageModel
{
    private readonly AppSettingsService _settings;
    private readonly IDocumentStorage _storage;

    public BrandingModel(AppSettingsService settings, IDocumentStorage storage)
    {
        _settings = settings;
        _storage = storage;
    }

    [BindProperty]
    public AppSettings Input { get; set; } = new();

    public AppSettings Current { get; private set; } = new();

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
        // Preserve fields not on this form
        Input.FromEmailOverride = Current.FromEmailOverride;
        Input.FromNameOverride = Current.FromNameOverride;
        Input.DefaultRentDueDay = Current.DefaultRentDueDay;
        Input.DefaultLateFeeGraceDays = Current.DefaultLateFeeGraceDays;
        Input.DefaultLateFeeAmount = Current.DefaultLateFeeAmount;
        Input.LogoStorageKey = Current.LogoStorageKey;
        Input.LogoSmallStorageKey = Current.LogoSmallStorageKey;

        if (!ModelState.IsValid) return Page();

        if (logoMain is { Length: > 0 })
        {
            Input.LogoStorageKey = await SaveLogoAsync(logoMain, "main");
        }
        if (logoSmall is { Length: > 0 })
        {
            Input.LogoSmallStorageKey = await SaveLogoAsync(logoSmall, "small");
        }

        await _settings.SaveAsync(Input);
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
