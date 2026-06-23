using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Payments;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<RentPayment> Payments { get; private set; } = new List<RentPayment>();

    public decimal CollectedThisMonth { get; private set; }
    public decimal CollectedYearToDate { get; private set; }
    public decimal AveragePayment { get; private set; }
    public decimal UnallocatedCredits { get; private set; }

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var firstOfYear = new DateOnly(today.Year, 1, 1);

        Payments = await _db.RentPayments
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Lease).ThenInclude(l => l!.Tenants)
            .Include(p => p.PaidByTenant)
            .Include(p => p.Allocations)
            .OrderByDescending(p => p.PaidOn)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        CollectedThisMonth = Payments.Where(p => p.PaidOn >= firstOfMonth).Sum(p => p.Amount);
        CollectedYearToDate = Payments.Where(p => p.PaidOn >= firstOfYear).Sum(p => p.Amount);
        AveragePayment = Payments.Count > 0 ? Payments.Average(p => p.Amount) : 0m;
        UnallocatedCredits = Payments.Sum(p => p.UnallocatedAmount);
    }
}
