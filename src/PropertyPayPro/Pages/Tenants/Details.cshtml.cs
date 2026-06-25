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
    public Lease? PrimaryLease { get; private set; }
    public List<RentPayment> RecentPayments { get; private set; } = new();
    public List<TenantUnpaidShare> UnpaidShares { get; private set; } = new();
    public List<EmailLog> CommunicationLog { get; private set; } = new();
    public List<GeneratedDocument> Documents { get; private set; } = new();

    public decimal LifetimePaid { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public int OnTimePercent { get; private set; }
    public int PaymentCount { get; private set; }

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
        PrimaryLease = ActiveLeases.FirstOrDefault() ?? Leases.FirstOrDefault();
        var leaseIds = Leases.Select(l => l.Id).ToList();

        var charges = await _db.RentalCharges
            .Where(c => leaseIds.Contains(c.LeaseId))
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .ToListAsync();

        // Compute this tenant's share of each charge.
        var shares = new List<TenantUnpaidShare>();
        foreach (var c in charges)
        {
            var tenantCount = Math.Max(1, c.Lease!.Tenants.Count);
            var share = Math.Round(c.AmountDue / tenantCount, 2);

            // Payments this tenant contributed toward this charge.
            var paidByThisTenant = c.Allocations
                .Where(a => a.Payment?.PaidByTenantId == Tenant.Id)
                .Sum(a => a.Amount);

            var balance = Math.Max(0m, share - paidByThisTenant);
            if (balance > 0)
            {
                shares.Add(new TenantUnpaidShare
                {
                    Charge = c,
                    TenantShare = share,
                    PaidByTenant = paidByThisTenant,
                    Balance = balance
                });
            }
        }

        UnpaidShares = shares.OrderBy(s => s.Charge.DueDate).ToList();
        CurrentBalance = UnpaidShares.Sum(s => s.Balance);

        // Lifetime paid by THIS tenant (using PaidByTenantId).
        LifetimePaid = await _db.RentPayments
            .Where(p => p.PaidByTenantId == Tenant.Id)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // On-time %: bills where this tenant's share is fully paid, and their last contribution was on/before due date.
        var paidShares = new List<(RentalCharge Charge, DateOnly? LastPaidOn)>();
        foreach (var c in charges)
        {
            var tenantCount = Math.Max(1, c.Lease!.Tenants.Count);
            var share = Math.Round(c.AmountDue / tenantCount, 2);
            var tenantAllocations = c.Allocations
                .Where(a => a.Payment?.PaidByTenantId == Tenant.Id)
                .ToList();
            var paidByThisTenant = tenantAllocations.Sum(a => a.Amount);
            if (paidByThisTenant >= share && tenantAllocations.Count > 0)
            {
                var lastPaid = tenantAllocations.Max(a => a.Payment?.PaidOn);
                paidShares.Add((c, lastPaid));
            }
        }
        if (paidShares.Count > 0)
        {
            var onTime = paidShares.Count(s => s.LastPaidOn.HasValue && s.LastPaidOn.Value <= s.Charge.DueDate);
            OnTimePercent = (int)Math.Round(onTime * 100.0 / paidShares.Count);
        }

        RecentPayments = await _db.RentPayments
            .Where(p => p.PaidByTenantId == Tenant.Id)
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Allocations)
            .OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id)
            .Take(15)
            .ToListAsync();

        PaymentCount = await _db.RentPayments.CountAsync(p => p.PaidByTenantId == Tenant.Id);

        CommunicationLog = await _db.EmailLogs
            .Where(e => e.LeaseId.HasValue && leaseIds.Contains(e.LeaseId.Value))
            .OrderByDescending(e => e.CreatedUtc)
            .Take(20)
            .ToListAsync();

        Documents = await _db.GeneratedDocuments
            .Where(d => d.LeaseId.HasValue && leaseIds.Contains(d.LeaseId.Value))
            .OrderByDescending(d => d.CreatedUtc)
            .Take(20)
            .ToListAsync();

        return Page();
    }

    public class TenantUnpaidShare
    {
        public RentalCharge Charge { get; set; } = null!;
        public decimal TenantShare { get; set; }
        public decimal PaidByTenant { get; set; }
        public decimal Balance { get; set; }
    }
}
