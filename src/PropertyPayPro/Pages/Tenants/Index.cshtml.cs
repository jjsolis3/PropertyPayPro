using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Tenants;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Tenant> Tenants { get; private set; } = new List<Tenant>();

    public int TotalCount { get; private set; }
    public int ActiveCount { get; private set; }
    public int WithOverdueCount { get; private set; }
    public int AvgTenureMonths { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Tenants = await _db.Tenants
            .Include(t => t.Leases)
            .OrderBy(t => t.LastName)
            .ToListAsync();
        TotalCount = Tenants.Count;

        var activeTenantIds = Tenants
            .Where(t => t.Leases.Any(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today)))
            .Select(t => t.Id)
            .ToHashSet();
        ActiveCount = activeTenantIds.Count;

        var overdueLeaseIds = await _db.RentalCharges
            .Include(c => c.Allocations)
            .ToListAsync();
        var overdueIds = overdueLeaseIds
            .Where(c => c.Status == ChargeStatus.Overdue)
            .Select(c => c.LeaseId)
            .ToHashSet();
        WithOverdueCount = Tenants.Count(t => t.Leases.Any(l => overdueIds.Contains(l.Id)));

        var tenuredLeases = Tenants
            .SelectMany(t => t.Leases)
            .Where(l => l.StartDate <= today)
            .ToList();
        if (tenuredLeases.Count > 0)
        {
            var totalMonths = tenuredLeases.Sum(l =>
            {
                var end = (!l.IsMonthToMonth && l.EndDate < today) ? l.EndDate : today;
                return Math.Max(0, ((end.Year - l.StartDate.Year) * 12) + end.Month - l.StartDate.Month);
            });
            AvgTenureMonths = totalMonths / tenuredLeases.Count;
        }
    }
}
