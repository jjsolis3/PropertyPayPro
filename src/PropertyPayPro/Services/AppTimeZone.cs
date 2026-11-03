namespace PropertyPayPro.Services;

/// <summary>
/// The business runs in a single time zone, so the configured zone is made
/// the process-wide local zone. That way DateTime.Now, ToLocalTime() and
/// DateTimeKind.Local conversions throughout the app all follow it, instead
/// of the container's UTC. On Linux, .NET resolves the local zone from the
/// TZ environment variable, re-read after the cache is cleared.
/// </summary>
public static class AppTimeZone
{
    public const string DefaultId = "America/Los_Angeles";

    /// <summary>Zones offered first in the picker, in the order shown.</summary>
    public static IReadOnlyList<(string Id, string Label)> Common { get; } = new[]
    {
        ("America/Los_Angeles", "Pacific Time (Los Angeles)"),
        ("America/Denver", "Mountain Time (Denver)"),
        ("America/Phoenix", "Arizona (Phoenix, no daylight saving)"),
        ("America/Chicago", "Central Time (Chicago)"),
        ("America/New_York", "Eastern Time (New York)"),
        ("America/Anchorage", "Alaska Time (Anchorage)"),
        ("Pacific/Honolulu", "Hawaii (Honolulu)"),
        ("UTC", "UTC"),
    };

    public static bool IsValid(string? id) => Find(id) is not null;

    public static TimeZoneInfo? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    /// <summary>Makes <paramref name="id"/> the process local zone. Returns false if unknown.</summary>
    public static bool Apply(string? id)
    {
        if (!IsValid(id)) return false;
        Environment.SetEnvironmentVariable("TZ", id);
        TimeZoneInfo.ClearCachedData();
        return true;
    }

    /// <summary>Every IANA zone on the server, for the full picker list.</summary>
    public static IEnumerable<string> AllIds() =>
        TimeZoneInfo.GetSystemTimeZones()
            .Select(z => z.Id)
            .Where(id => id.Contains('/') || id == "UTC")
            .OrderBy(id => id, StringComparer.Ordinal);
}
