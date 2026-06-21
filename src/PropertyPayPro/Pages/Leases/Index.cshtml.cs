using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Leases;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Lease> Leases { get; private set; } = new List<Lease>();

    public int ActiveCount { get; private set; }
    public int ExpiringSoonCount { get; private set; }
    public decimal MonthlyRentRoll { get; private set; }
    public decimal UnpaidTotal { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(30);

        Leases = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();

        var active = Leases
            .Where(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today))
            .ToList();

        ActiveCount = active.Count;
        ExpiringSoonCount = active.Count(l => !l.IsMonthToMonth && l.EndDate <= soon);
        MonthlyRentRoll = active.Sum(l => l.MonthlyRent);

        var activeIds = active.Select(l => l.Id).ToHashSet();
        var charges = await _db.RentalCharges
            .Where(c => activeIds.Contains(c.LeaseId))
            .Include(c => c.Allocations)
            .ToListAsync();
        UnpaidTotal = charges.Sum(c => c.Balance);
    }
}
