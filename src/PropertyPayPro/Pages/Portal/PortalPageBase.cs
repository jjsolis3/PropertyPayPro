using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Portal;

/// <summary>
/// Base class for all Portal pages. Enforces the Tenant role, resolves the
/// current user's linked Tenant record, and provides helpers to load
/// tenant-scoped data with the "share of a bill" math already applied.
/// </summary>
[Authorize(Roles = IdentitySeed.TenantRole)]
public abstract class PortalPageBase : PageModel
{
    protected readonly ApplicationDbContext Db;
    protected readonly UserManager<ApplicationUser> Users;

    protected PortalPageBase(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        Db = db;
        Users = users;
    }

    public Tenant? CurrentTenant { get; private set; }
    public List<Lease> AllLeases { get; private set; } = new();
    public List<Lease> ActiveLeases { get; private set; } = new();
    public List<int> ActivePropertyIds { get; private set; } = new();

    /// <summary>
    /// Loads the current user's Tenant record and its leases. Every Portal
    /// page should call this in OnGetAsync. Returns null if we found the
    /// tenant (page can proceed) or an IActionResult to short-circuit
    /// (Forbid / NotFound) if the login isn't properly linked to a tenant.
    /// </summary>
    protected async Task<IActionResult?> LoadCurrentTenantAsync()
    {
        var user = await Users.GetUserAsync(User);
        if (user is null || !user.TenantId.HasValue) return Forbid();

        CurrentTenant = await Db.Tenants
            .Include(t => t.Leases).ThenInclude(l => l.Property)
            .Include(t => t.Leases).ThenInclude(l => l.Tenants)
            .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value);

        if (CurrentTenant is null) return Forbid();

        AllLeases = CurrentTenant.Leases.OrderByDescending(l => l.StartDate).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ActiveLeases = AllLeases
            .Where(l => l.StartDate <= today && (l.IsMonthToMonth || l.EndDate >= today))
            .ToList();
        ActivePropertyIds = ActiveLeases.Select(l => l.PropertyId).Distinct().ToList();

        return null;
    }

    /// <summary>
    /// Compute this tenant's share of each charge (AmountDue / #tenants on
    /// lease) minus what they've personally paid toward it. Returns unpaid
    /// shares only, sorted by due date. Mirrors the math in
    /// /Tenants/Details for the admin-facing view.
    /// </summary>
    protected async Task<List<TenantUnpaidShare>> GetUnpaidSharesAsync()
    {
        if (CurrentTenant is null) return new();

        var leaseIds = AllLeases.Select(l => l.Id).ToList();
        var charges = await Db.RentalCharges
            .Where(c => leaseIds.Contains(c.LeaseId))
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .ToListAsync();

        var shares = new List<TenantUnpaidShare>();
        foreach (var c in charges)
        {
            var tenantCount = Math.Max(1, c.Lease!.Tenants.Count);
            var share = Math.Round(c.AmountDue / tenantCount, 2);
            var paidByThisTenant = c.Allocations
                .Where(a => a.Payment?.PaidByTenantId == CurrentTenant.Id)
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
        return shares.OrderBy(s => s.Charge.DueDate).ToList();
    }

    public class TenantUnpaidShare
    {
        public RentalCharge Charge { get; set; } = null!;
        public decimal TenantShare { get; set; }
        public decimal PaidByTenant { get; set; }
        public decimal Balance { get; set; }
    }
}
