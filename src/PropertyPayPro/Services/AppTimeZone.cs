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
        NativeTimeZone.Set(id!);
        TimeZoneInfo.ClearCachedData();
        return true;
    }

    /// <summary>
    /// Environment.SetEnvironmentVariable only changes .NET's own copy of the
    /// environment. Native code in the same process (OpenSSL, used to validate TLS
    /// certificates when sending email) kept the container's zone (UTC), so it
    /// disagreed with .NET's local time by the zone offset (8 hours for Los Angeles).
    /// The visible symptom: a mail server certificate issued in the last 8 hours was
    /// rejected as "not yet valid", so email failed after a provider rotated certs.
    /// Setting TZ natively too, then tzset(), keeps both sides in step.
    /// </summary>
    private static class NativeTimeZone
    {
        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "setenv")]
        private static extern int SetEnv(string name, string value, int overwrite);

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "tzset")]
        private static extern void TzSet();

        public static void Set(string id)
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
            try
            {
                SetEnv("TZ", id, 1);
                TzSet();
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                // No libc to call (unusual platform): .NET's own time zone still applies.
            }
        }
    }

    /// <summary>Every IANA zone on the server, for the full picker list.</summary>
    public static IEnumerable<string> AllIds() =>
        TimeZoneInfo.GetSystemTimeZones()
            .Select(z => z.Id)
            .Where(id => id.Contains('/') || id == "UTC")
            .OrderBy(id => id, StringComparer.Ordinal);
}
