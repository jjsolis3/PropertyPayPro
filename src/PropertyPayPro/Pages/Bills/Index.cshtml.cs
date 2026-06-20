using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public ChargeStatus? StatusFilter { get; set; }

    public IList<RentalCharge> Charges { get; private set; } = new List<RentalCharge>();

    public async Task OnGetAsync()
    {
        var query = _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenant)
            .Include(c => c.Allocations)
            .OrderByDescending(c => c.BillingPeriodStart)
            .AsQueryable();

        var list = await query.ToListAsync();
        if (StatusFilter.HasValue)
        {
            list = list.Where(c => c.Status == StatusFilter.Value).ToList();
        }
        Charges = list;
    }
}
