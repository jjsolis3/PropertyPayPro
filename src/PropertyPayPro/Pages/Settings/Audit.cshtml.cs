using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Settings;

/// <summary>
/// Admin-only paged view over the AuditEntries table. Covered by the
/// /Settings folder-level AdminOnly policy — no [Authorize] needed here.
/// </summary>
public class AuditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private const int PageSize = 100;

    public AuditModel(ApplicationDbContext db) { _db = db; }

    [BindProperty(SupportsGet = true)] public string? ActionFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? UserFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityType { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromLocal { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToLocal { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public List<AuditEntry> Entries { get; private set; } = new();
    public List<string> KnownActions { get; private set; } = new();
    public List<string> KnownEntityTypes { get; private set; } = new();
    public int Total { get; private set; }
    public bool HasPrev => PageNumber > 1;
    public bool HasNext => PageNumber * PageSize < Total;

    public async Task OnGetAsync()
    {
        ViewData["SettingsTab"] = "audit";

        // Cheap distinct lists for the dropdown filters. The audit
        // table doesn't grow large enough per month to need caching.
        KnownActions = await _db.AuditEntries
            .Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
        KnownEntityTypes = await _db.AuditEntries
            .Where(a => a.EntityType != null)
            .Select(a => a.EntityType!).Distinct().OrderBy(a => a).ToListAsync();

        var q = _db.AuditEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(ActionFilter)) q = q.Where(a => a.Action == ActionFilter);
        if (!string.IsNullOrWhiteSpace(EntityType)) q = q.Where(a => a.EntityType == EntityType);
        if (!string.IsNullOrWhiteSpace(UserFilter))
        {
            var needle = UserFilter.Trim().ToLower();
            q = q.Where(a => (a.UserEmail ?? "").ToLower().Contains(needle));
        }
        if (FromLocal.HasValue)
        {
            var utc = DateTime.SpecifyKind(FromLocal.Value, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(a => a.CreatedUtc >= utc);
        }
        if (ToLocal.HasValue)
        {
            var utc = DateTime.SpecifyKind(ToLocal.Value, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(a => a.CreatedUtc <= utc);
        }

        Total = await q.CountAsync();
        if (PageNumber < 1) PageNumber = 1;

        Entries = await q.OrderByDescending(a => a.CreatedUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
