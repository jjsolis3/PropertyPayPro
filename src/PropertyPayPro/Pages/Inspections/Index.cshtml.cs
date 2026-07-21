using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Inspections;

[Authorize(Roles = IdentitySeed.AdminRole + "," + IdentitySeed.ManagerRole)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public InspectionKind? KindFilter { get; set; }
    [BindProperty(SupportsGet = true)] public InspectionStatus? StatusFilter { get; set; }
    [BindProperty(SupportsGet = true)] public int? PropertyFilter { get; set; }

    public List<Inspection> Inspections { get; private set; } = new();
    public List<Property> Properties { get; private set; } = new();

    public int TotalCount { get; private set; }
    public int DraftCount { get; private set; }
    public int CompletedThisYearCount { get; private set; }

    public async Task OnGetAsync()
    {
        Properties = await _db.Properties.OrderBy(p => p.Name).ToListAsync();

        var q = _db.Inspections
            .Include(i => i.Lease).ThenInclude(l => l!.Property)
            .Include(i => i.Lease).ThenInclude(l => l!.Tenants)
            .AsQueryable();

        if (KindFilter.HasValue) q = q.Where(i => i.Kind == KindFilter.Value);
        if (StatusFilter.HasValue) q = q.Where(i => i.Status == StatusFilter.Value);
        if (PropertyFilter.HasValue) q = q.Where(i => i.Lease!.PropertyId == PropertyFilter.Value);

        Inspections = await q
            .OrderByDescending(i => i.ScheduledFor)
            .ThenByDescending(i => i.Id)
            .Take(500)
            .ToListAsync();

        TotalCount = await _db.Inspections.CountAsync();
        DraftCount = await _db.Inspections.CountAsync(i => i.Status == InspectionStatus.Draft);
        var yearStart = new DateOnly(DateTime.UtcNow.Year, 1, 1);
        CompletedThisYearCount = await _db.Inspections
            .CountAsync(i => i.CompletedOn.HasValue && i.CompletedOn.Value >= yearStart);
    }
}
