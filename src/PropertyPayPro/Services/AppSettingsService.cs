using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

public class AppSettingsService
{
    public const int SingletonId = 1;
    private const string CacheKey = "AppSettings.Singleton";

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public AppSettingsService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<AppSettings> GetAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out AppSettings? cached) && cached is not null)
            return cached;

        var settings = await _db.AppSettings.FirstOrDefaultAsync(s => s.Id == SingletonId, ct);
        if (settings is null)
        {
            settings = new AppSettings { Id = SingletonId };
            _db.AppSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
        }
        _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(10));
        return settings;
    }

    public async Task SaveAsync(AppSettings updated, CancellationToken ct = default)
    {
        var existing = await _db.AppSettings.FirstOrDefaultAsync(s => s.Id == SingletonId, ct);
        if (existing is null)
        {
            updated.Id = SingletonId;
            updated.UpdatedUtc = DateTime.UtcNow;
            _db.AppSettings.Add(updated);
        }
        else
        {
            existing.AppName = updated.AppName;
            existing.PrimaryColor = updated.PrimaryColor;
            existing.AccentColor = updated.AccentColor;
            existing.LogoStorageKey = updated.LogoStorageKey;
            existing.LogoSmallStorageKey = updated.LogoSmallStorageKey;
            existing.PublicBaseUrl = updated.PublicBaseUrl;
            existing.FromEmailOverride = updated.FromEmailOverride;
            existing.FromNameOverride = updated.FromNameOverride;
            existing.DefaultRentDueDay = updated.DefaultRentDueDay;
            existing.DefaultLateFeeGraceDays = updated.DefaultLateFeeGraceDays;
            existing.DefaultLateFeeAmount = updated.DefaultLateFeeAmount;
            existing.UpdatedUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }

    public void InvalidateCache() => _cache.Remove(CacheKey);

    /// <summary>
    /// Site URL for emails sent outside a web request. Uses the Branding
    /// setting, then Coolify's injected COOLIFY_URL (first entry if it holds
    /// several), else empty, in which case emails go out without the logo.
    /// </summary>
    public async Task<string> GetPublicBaseUrlAsync(CancellationToken ct = default)
    {
        var settings = await GetAsync(ct);
        var url = settings.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(url))
            url = Environment.GetEnvironmentVariable("COOLIFY_URL")?.Split(',')[0];
        return url?.Trim().TrimEnd('/') ?? "";
    }
}
