using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

/// <summary>
/// Append-only audit log for sensitive admin/manager actions.
/// Nothing in this service updates or deletes rows at runtime; the
/// table is immutable from the app's point of view, and shrinking it
/// is left to ops (manual archive / retention).
/// </summary>
public interface IAuditLog
{
    Task WriteAsync(
        string action,
        string? entityType = null,
        int? entityId = null,
        string? details = null,
        string? overrideActor = null,
        CancellationToken ct = default);
}

public class AuditLog : IAuditLog
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditLog> _logger;

    public AuditLog(ApplicationDbContext db, IHttpContextAccessor http, ILogger<AuditLog> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action,
        string? entityType = null,
        int? entityId = null,
        string? details = null,
        string? overrideActor = null,
        CancellationToken ct = default)
    {
        var user = _http.HttpContext?.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = overrideActor
            ?? user?.FindFirstValue(ClaimTypes.Email)
            ?? user?.Identity?.Name
            ?? "system";

        var ip = _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        var entry = new AuditEntry
        {
            CreatedUtc = DateTime.UtcNow,
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = Trim(details, 4000),
            IpAddress = Trim(ip, 64),
        };

        try
        {
            _db.AuditEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // The audit table should never block the action it's logging —
            // if the write fails we swallow it and log to the standard
            // logger, so Create/Edit/Send still succeed.
            _logger.LogWarning(ex,
                "Audit write failed for action {Action} on {EntityType}#{EntityId}",
                action, entityType, entityId);
        }
    }

    private static string? Trim(string? s, int max) =>
        s is null ? null : (s.Length <= max ? s : s[..max]);
}
