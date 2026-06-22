using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Tenants;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Tenant? Tenant { get; private set; }

    public List<Lease> Leases { get; private set; } = new();
    public List<Lease> ActiveLeases { get; private set; } = new();
    public List<RentPayment> RecentPayments { get; private set; } = new();
    public List<RentalCharge> UnpaidBills { get; private set; } = new();
    public List<EmailLog> CommunicationLog { get; private set; } = new();
    public List<GeneratedDocument> Documents { get; private set; } = new();

    public decimal LifetimePaid { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public int OnTimePercent { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Tenant = await _db.Tenants
            .Include(t => t.Leases).ThenInclude(l => l.Property)
            .Include(t => t.Leases).ThenInclude(l => l.Tenants)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (Tenant is null) return NotFound();

        Leases = Tenant.Leases.OrderByDescending(l => l.StartDate).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ActiveLeases = Leases.Where(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today)).ToList();
        var leaseIds = Leases.Select(l => l.Id).ToList();

        var charges = await _db.RentalCharges
            .Where(c => leaseIds.Contains(c.LeaseId))
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .ToListAsync();

        UnpaidBills = charges.Where(c => c.Balance > 0).OrderBy(c => c.DueDate).ToList();
        CurrentBalance = UnpaidBills.Sum(c => c.Balance);
        LifetimePaid = charges.SelectMany(c => c.Allocations).Sum(a => a.Amount);

        var paidBills = charges.Where(c => c.AmountPaid >= c.AmountDue).ToList();
        if (paidBills.Count > 0)
        {
            var onTime = paidBills.Count(c =>
                c.Allocations.Any() &&
                c.Allocations.Max(a => a.Payment?.PaidOn) <= c.DueDate);
            OnTimePercent = (int)Math.Round(onTime * 100.0 / paidBills.Count);
        }

        RecentPayments = await _db.RentPayments
            .Where(p => leaseIds.Contains(p.LeaseId))
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Allocations)
            .OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id)
            .Take(10)
            .ToListAsync();

        CommunicationLog = await _db.EmailLogs
            .Where(e => e.LeaseId.HasValue && leaseIds.Contains(e.LeaseId.Value))
            .OrderByDescending(e => e.CreatedUtc)
            .Take(15)
            .ToListAsync();

        Documents = await _db.GeneratedDocuments
            .Where(d => d.LeaseId.HasValue && leaseIds.Contains(d.LeaseId.Value))
            .OrderByDescending(d => d.CreatedUtc)
            .Take(20)
            .ToListAsync();

        return Page();
    }
}
