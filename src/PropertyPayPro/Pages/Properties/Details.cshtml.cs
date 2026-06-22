using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Properties;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Property? Property { get; private set; }
    public List<Lease> Leases { get; private set; } = new();
    public List<Lease> ActiveLeases { get; private set; } = new();
    public List<RentalCharge> UnpaidBills { get; private set; } = new();
    public List<PropertyExpense> RecentExpenses { get; private set; } = new();
    public List<ServiceTicket> OpenTickets { get; private set; } = new();

    public decimal MonthlyRentRoll { get; private set; }
    public decimal Outstanding { get; private set; }
    public decimal CollectedYearToDate { get; private set; }
    public decimal SpentYearToDate { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id);
        if (Property is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfYear = new DateOnly(today.Year, 1, 1);

        Leases = await _db.Leases
            .Where(l => l.PropertyId == id)
            .Include(l => l.Tenants)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();
        ActiveLeases = Leases
            .Where(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today))
            .ToList();
        MonthlyRentRoll = ActiveLeases.Sum(l => l.MonthlyRent);

        var leaseIds = Leases.Select(l => l.Id).ToList();
        var charges = await _db.RentalCharges
            .Where(c => leaseIds.Contains(c.LeaseId))
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .ToListAsync();

        UnpaidBills = charges.Where(c => c.Balance > 0).OrderBy(c => c.DueDate).ToList();
        Outstanding = UnpaidBills.Sum(c => c.Balance);
        CollectedYearToDate = charges
            .SelectMany(c => c.Allocations)
            .Where(a => a.Payment != null && a.Payment.PaidOn >= firstOfYear)
            .Sum(a => a.Amount);

        RecentExpenses = await _db.PropertyExpenses
            .Where(e => e.PropertyId == id)
            .OrderByDescending(e => e.DueDate)
            .ThenByDescending(e => e.Id)
            .Take(10)
            .ToListAsync();
        SpentYearToDate = await _db.PropertyExpenses
            .Where(e => e.PropertyId == id && e.PaidOn.HasValue && e.PaidOn.Value >= firstOfYear)
            .SumAsync(e => (decimal?)e.AmountDue) ?? 0m;

        OpenTickets = await _db.ServiceTickets
            .Where(t => t.PropertyId == id
                && (t.Status == ServiceTicketStatus.Open || t.Status == ServiceTicketStatus.InProgress))
            .OrderByDescending(t => t.ReportedOn)
            .ToListAsync();

        return Page();
    }
}
