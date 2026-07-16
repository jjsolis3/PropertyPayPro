using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Portal;

public class BillsModel : PortalPageBase
{
    public BillsModel(ApplicationDbContext db, UserManager<ApplicationUser> users)
        : base(db, users) { }

    public class BillRow
    {
        public RentalCharge Charge { get; set; } = null!;
        public decimal TenantShare { get; set; }
        public decimal PaidByTenant { get; set; }
        public decimal Balance { get; set; }
    }

    public List<BillRow> Rows { get; private set; } = new();
    public decimal TotalShare { get; private set; }
    public decimal TotalPaid { get; private set; }
    public decimal TotalBalance { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var sc = await LoadCurrentTenantAsync();
        if (sc is not null) return sc;

        var leaseIds = AllLeases.Select(l => l.Id).ToList();
        var charges = await Db.RentalCharges
            .Where(c => leaseIds.Contains(c.LeaseId))
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .OrderByDescending(c => c.DueDate)
            .ToListAsync();

        foreach (var c in charges)
        {
            var tenantCount = Math.Max(1, c.Lease!.Tenants.Count);
            var share = Math.Round(c.AmountDue / tenantCount, 2);
            var paid = c.Allocations
                .Where(a => a.Payment?.PaidByTenantId == CurrentTenant!.Id)
                .Sum(a => a.Amount);
            Rows.Add(new BillRow
            {
                Charge = c,
                TenantShare = share,
                PaidByTenant = paid,
                Balance = Math.Max(0m, share - paid)
            });
        }

        TotalShare = Rows.Sum(r => r.TenantShare);
        TotalPaid = Rows.Sum(r => r.PaidByTenant);
        TotalBalance = Rows.Sum(r => r.Balance);

        return Page();
    }
}
