using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Properties;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Property> Properties { get; private set; } = new List<Property>();

    public int TotalCount { get; private set; }
    public int OccupiedCount { get; private set; }
    public int VacantCount { get; private set; }
    public decimal RentRoll { get; private set; }
    public decimal Outstanding { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Properties = await _db.Properties.OrderBy(p => p.Name).ToListAsync();
        TotalCount = Properties.Count;

        var activeLeases = await _db.Leases
            .Where(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today))
            .ToListAsync();

        OccupiedCount = activeLeases.Select(l => l.PropertyId).Distinct().Count();
        VacantCount = Math.Max(0, TotalCount - OccupiedCount);
        RentRoll = activeLeases.Sum(l => l.MonthlyRent);

        var charges = await _db.RentalCharges.Include(c => c.Allocations).ToListAsync();
        Outstanding = charges.Sum(c => c.Balance);
    }
}
