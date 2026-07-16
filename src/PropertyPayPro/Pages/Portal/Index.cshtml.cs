using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Portal;

public class IndexModel : PortalPageBase
{
    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
        : base(db, users) { }

    public List<TenantUnpaidShare> UnpaidShares { get; private set; } = new();
    public List<RentPayment> RecentPayments { get; private set; } = new();

    public decimal LifetimePaid { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public int PaymentCount { get; private set; }
    public int OnTimePercent { get; private set; }
    public Lease? PrimaryLease { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var short_circuit = await LoadCurrentTenantAsync();
        if (short_circuit is not null) return short_circuit;

        PrimaryLease = ActiveLeases.FirstOrDefault() ?? AllLeases.FirstOrDefault();

        UnpaidShares = await GetUnpaidSharesAsync();
        CurrentBalance = UnpaidShares.Sum(s => s.Balance);

        LifetimePaid = await Db.RentPayments
            .Where(p => p.PaidByTenantId == CurrentTenant!.Id)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        PaymentCount = await Db.RentPayments
            .CountAsync(p => p.PaidByTenantId == CurrentTenant!.Id);

        RecentPayments = await Db.RentPayments
            .Where(p => p.PaidByTenantId == CurrentTenant!.Id)
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id)
            .Take(5)
            .ToListAsync();

        // On-time %: fully-paid shares where last contribution was on/before due date.
        var leaseIds = AllLeases.Select(l => l.Id).ToList();
        var chargesForOnTime = await Db.RentalCharges
            .Where(c => leaseIds.Contains(c.LeaseId))
            .Include(c => c.Lease!).ThenInclude(l => l.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .ToListAsync();

        var paidShares = new List<(RentalCharge Charge, DateOnly? LastPaidOn)>();
        foreach (var c in chargesForOnTime)
        {
            var tenantCount = Math.Max(1, c.Lease!.Tenants.Count);
            var share = Math.Round(c.AmountDue / tenantCount, 2);
            var tenantAllocs = c.Allocations
                .Where(a => a.Payment?.PaidByTenantId == CurrentTenant!.Id)
                .ToList();
            var paidByThisTenant = tenantAllocs.Sum(a => a.Amount);
            if (paidByThisTenant >= share && tenantAllocs.Count > 0)
            {
                paidShares.Add((c, tenantAllocs.Max(a => a.Payment?.PaidOn)));
            }
        }
        if (paidShares.Count > 0)
        {
            var onTime = paidShares.Count(s => s.LastPaidOn.HasValue && s.LastPaidOn.Value <= s.Charge.DueDate);
            OnTimePercent = (int)Math.Round(onTime * 100.0 / paidShares.Count);
        }

        return Page();
    }
}
