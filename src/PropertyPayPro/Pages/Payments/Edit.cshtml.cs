using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Payments;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly BillingService _billing;
    public EditModel(ApplicationDbContext db, BillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    [BindProperty]
    public RentPayment Payment { get; set; } = new();

    [BindProperty]
    public List<int> AllocationChargeIds { get; set; } = new();

    [BindProperty]
    public List<decimal> AllocationAmounts { get; set; } = new();

    public SelectList Leases { get; private set; } = default!;
    public List<RentalCharge> OutstandingCharges { get; private set; } = new();
    public Dictionary<int, decimal> CurrentAllocations { get; private set; } = new();
    public Dictionary<int, List<TenantOption>> LeaseTenants { get; private set; } = new();

    public record TenantOption(int Id, string DisplayName);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.RentPayments
            .Include(p => p.Allocations).ThenInclude(a => a.RentalCharge)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();
        Payment = entity;

        CurrentAllocations = entity.Allocations
            .ToDictionary(a => a.RentalChargeId, a => a.Amount);

        await LoadSelectListAsync();
        await LoadOutstandingAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _db.RentPayments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == Payment.Id);
        if (existing is null) return NotFound();

        // Moving the payment to another lease: the allocation rows on the form are the
        // old lease's bills, so clear them and send the admin back to pick the new
        // lease's bills (previously they stayed attached to the old lease's bills).
        var leaseChanged = existing.LeaseId != Payment.LeaseId;
        if (leaseChanged)
        {
            AllocationChargeIds.Clear();
            AllocationAmounts.Clear();
        }

        if (ModelState.IsValid)
        {
            var totalAllocated = AllocationAmounts.Where(a => a > 0).Sum();
            if (totalAllocated > Payment.Amount)
                ModelState.AddModelError(string.Empty,
                    $"Total allocated ({totalAllocated:C}) exceeds payment amount ({Payment.Amount:C}).");
            else if (await _billing.ValidateAllocationsAsync(Payment.LeaseId, AllocationChargeIds, AllocationAmounts, existing.Id) is string allocationError)
                ModelState.AddModelError(string.Empty, allocationError);
        }
        if (!ModelState.IsValid)
        {
            // Redisplay what was entered (the view reads CurrentAllocations); before,
            // this was empty, so saving again wiped the payment's allocations.
            CurrentAllocations = new Dictionary<int, decimal>();
            for (var i = 0; i < AllocationChargeIds.Count && i < AllocationAmounts.Count; i++)
                CurrentAllocations[AllocationChargeIds[i]] = AllocationAmounts[i];
            if (CurrentAllocations.Count == 0)
                CurrentAllocations = existing.Allocations.ToDictionary(a => a.RentalChargeId, a => a.Amount);
            await LoadSelectListAsync();
            await LoadOutstandingAsync();
            return Page();
        }

        existing.LeaseId = Payment.LeaseId;
        existing.PaidByTenantId = Payment.PaidByTenantId;
        existing.PaidOn = Payment.PaidOn;
        existing.Amount = Payment.Amount;
        existing.Method = Payment.Method;
        existing.Reference = Payment.Reference;
        existing.Notes = Payment.Notes;

        // Rebuild allocations
        _db.PaymentAllocations.RemoveRange(existing.Allocations);
        for (var i = 0; i < AllocationChargeIds.Count && i < AllocationAmounts.Count; i++)
        {
            if (AllocationAmounts[i] <= 0) continue;
            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                RentPaymentId = existing.Id,
                RentalChargeId = AllocationChargeIds[i],
                Amount = AllocationAmounts[i]
            });
        }

        await _db.SaveChangesAsync();
        if (leaseChanged)
        {
            TempData["Message"] = "Lease changed. Its old bill allocations were removed: pick the new lease's bills below and save.";
            return RedirectToPage(new { id = existing.Id });
        }
        return RedirectToPage("/Receipts/Show", new { id = existing.Id });
    }

    private async Task LoadSelectListAsync()
    {
        var leases = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();

        Leases = new SelectList(
            leases.Select(l => new { l.Id, Label = $"{l.TenantNames} — {l.Property!.Name}" }),
            "Id", "Label");

        LeaseTenants = leases.ToDictionary(
            l => l.Id,
            l => l.Tenants.OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
                          .Select(t => new TenantOption(t.Id, t.DisplayName)).ToList());
    }

    private async Task LoadOutstandingAsync()
    {
        if (Payment.LeaseId == 0) return;

        var charges = await _db.RentalCharges
            .Where(c => c.LeaseId == Payment.LeaseId)
            .Include(c => c.Allocations)
            .OrderBy(c => c.BillingPeriodStart)
            .ThenBy(c => c.Kind)
            .ToListAsync();

        // Include charges with balance > 0 OR charges already allocated from this payment
        OutstandingCharges = charges
            .Where(c => c.Balance > 0 || CurrentAllocations.ContainsKey(c.Id))
            .ToList();
    }
}
