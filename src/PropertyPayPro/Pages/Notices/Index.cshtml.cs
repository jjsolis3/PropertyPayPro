using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Notices;

/// <summary>
/// Cross-lease list of previously generated notices. Notices are stored
/// as LeaseDocument{Type=Notice} rows so this view is just a filtered
/// projection of that table — no separate "notice" table needed.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole + "," + IdentitySeed.ManagerRole)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;

    public IndexModel(ApplicationDbContext db, IDocumentStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty(SupportsGet = true)]
    public int? PropertyFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public List<LeaseDocument> Notices { get; private set; } = new();
    public List<Property> Properties { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int Last30DaysCount { get; private set; }
    public int SentCount { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!User.IsInRole(IdentitySeed.AdminRole)) return Forbid();

        var doc = await _db.LeaseDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.Type == LeaseDocumentType.Notice);
        if (doc is null)
        {
            TempData["Error"] = "That notice no longer exists.";
            return RedirectToPage();
        }

        // Best-effort delete of the stored PDF; missing-file errors are fine.
        try { await _storage.DeleteAsync(doc.StorageKey); } catch { /* ignore */ }

        _db.LeaseDocuments.Remove(doc);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Notice \"{doc.Description ?? doc.FileName}\" deleted.";
        return RedirectToPage(new { PropertyFilter, Q });
    }

    private async Task LoadAsync()
    {
        Properties = await _db.Properties.OrderBy(p => p.Name).ToListAsync();

        var q = _db.LeaseDocuments
            .Include(d => d.Lease).ThenInclude(l => l!.Property)
            .Include(d => d.Lease).ThenInclude(l => l!.Tenants)
            .Where(d => d.Type == LeaseDocumentType.Notice);

        if (PropertyFilter.HasValue)
            q = q.Where(d => d.Lease!.PropertyId == PropertyFilter.Value);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var needle = Q.Trim().ToLower();
            q = q.Where(d =>
                d.FileName.ToLower().Contains(needle)
                || (d.Description ?? "").ToLower().Contains(needle)
                || d.Lease!.Property!.Name.ToLower().Contains(needle));
        }

        Notices = await q.OrderByDescending(d => d.UploadedOn).Take(500).ToListAsync();
        TotalCount = await _db.LeaseDocuments.CountAsync(d => d.Type == LeaseDocumentType.Notice);
        var cutoff = DateTime.UtcNow.AddDays(-30);
        Last30DaysCount = await _db.LeaseDocuments
            .CountAsync(d => d.Type == LeaseDocumentType.Notice && d.UploadedOn >= cutoff);
        SentCount = await _db.LeaseDocuments
            .CountAsync(d => d.Type == LeaseDocumentType.Notice && d.LastSentUtc != null);
    }
}
