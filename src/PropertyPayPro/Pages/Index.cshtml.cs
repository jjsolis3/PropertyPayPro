using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) => _db = db;

    public int PropertyCount { get; private set; }
    public int ActiveLeaseCount { get; private set; }
    public decimal CollectedThisMonth { get; private set; }
    public decimal OutstandingBalance { get; private set; }
    public int OverdueCount { get; private set; }
    public List<RentPayment> RecentPayments { get; private set; } = new();
    public List<RentalCharge> UpcomingBills { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        PropertyCount = await _db.Properties.CountAsync();
        ActiveLeaseCount = await _db.Leases
            .CountAsync(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today));
        CollectedThisMonth = await _db.RentPayments
            .Where(p => p.PaidOn >= firstOfMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var charges = await _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Tenant)
            .Include(c => c.Allocations)
            .ToListAsync();

        OutstandingBalance = charges.Sum(c => c.Balance);
        OverdueCount = charges.Count(c => c.Status == ChargeStatus.Overdue);

        UpcomingBills = charges
            .Where(c => c.Balance > 0)
            .OrderBy(c => c.DueDate)
            .Take(5)
            .ToList();

        RecentPayments = await _db.RentPayments
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Lease).ThenInclude(l => l!.Tenant)
            .OrderByDescending(p => p.PaidOn)
            .ThenByDescending(p => p.Id)
            .Take(8)
            .ToListAsync();
    }
}
