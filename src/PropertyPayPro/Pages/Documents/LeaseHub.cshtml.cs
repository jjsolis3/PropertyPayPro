using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Pages.Shared;

namespace PropertyPayPro.Pages.Documents;

/// <summary>
/// Cross-lease Documents Hub. Aggregates every LeaseDocument in the
/// system into one searchable, filterable table so admins and managers
/// don't have to click into each lease to find "Amar's 2025 renewal"
/// or "the insurance certs expiring next month."
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole + "," + IdentitySeed.ManagerRole)]
public class LeaseHubModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public LeaseHubModel(ApplicationDbContext db) { _db = db; }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public LeaseDocumentType? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? PropertyFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public List<LeaseDocument> Documents { get; private set; } = new();
    public List<Property> Properties { get; private set; } = new();

    public int TotalCount { get; private set; }
    public int ExpiringSoonCount { get; private set; }
    public int ExpiredCount { get; private set; }
    public List<LeaseDocument> ExpiringSoon { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Properties = await _db.Properties.OrderBy(p => p.Name).ToListAsync();

        var q = _db.LeaseDocuments
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .Include(d => d.Lease).ThenInclude(l => l!.Tenants)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var needle = Q.Trim().ToLower();
            q = q.Where(d =>
                d.FileName.ToLower().Contains(needle)
                || (d.Description ?? "").ToLower().Contains(needle)
                || d.Lease!.Property!.Name.ToLower().Contains(needle));
        }
        if (TypeFilter.HasValue) q = q.Where(d => d.Type == TypeFilter.Value);
        if (PropertyFilter.HasValue) q = q.Where(d => d.Lease!.PropertyId == PropertyFilter.Value);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soonCutoff = today.AddDays(30);
        if (Status == "expiring")
        {
            q = q.Where(d => d.ExpiresOn.HasValue
                && d.ExpiresOn.Value >= today
                && d.ExpiresOn.Value <= soonCutoff);
        }
        else if (Status == "expired")
        {
            q = q.Where(d => d.ExpiresOn.HasValue && d.ExpiresOn.Value < today);
        }
        else if (Status == "current")
        {
            q = q.Where(d => !d.ExpiresOn.HasValue || d.ExpiresOn.Value > soonCutoff);
        }

        Documents = await q
            .OrderByDescending(d => d.UploadedOn)
            .Take(1000)
            .ToListAsync();

        // Totals (independent of the current filter — dashboard-style
        // counts across the whole system).
        var all = await _db.LeaseDocuments
            .Select(d => new { d.ExpiresOn })
            .ToListAsync();
        TotalCount = all.Count;
        ExpiringSoonCount = all.Count(d => d.ExpiresOn.HasValue
            && d.ExpiresOn.Value >= today
            && d.ExpiresOn.Value <= soonCutoff);
        ExpiredCount = all.Count(d => d.ExpiresOn.HasValue && d.ExpiresOn.Value < today);

        ExpiringSoon = await _db.LeaseDocuments
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .Where(d => d.ExpiresOn.HasValue
                && d.ExpiresOn.Value >= today
                && d.ExpiresOn.Value <= soonCutoff)
            .OrderBy(d => d.ExpiresOn)
            .Take(10)
            .ToListAsync();
    }
}
