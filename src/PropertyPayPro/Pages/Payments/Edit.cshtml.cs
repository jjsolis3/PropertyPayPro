using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Payments;

[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public RentPayment Payment { get; set; } = new();

    [BindProperty]
    public List<int> AllocationChargeIds { get; set; } = new();

    [BindProperty]
    public List<decimal> AllocationAmounts { get; set; } = new();

    public SelectList Leases { get; private set; } = default!;
    public List<RentalCharge> OutstandingCharges { get; private set; } = new();
    public Dictionary<int, decimal> CurrentAllocations { get; private set; } = new();

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
        if (!ModelState.IsValid)
        {
            await LoadSelectListAsync();
            await LoadOutstandingAsync();
            return Page();
        }

        var totalAllocated = AllocationAmounts.Where(a => a > 0).Sum();
        if (totalAllocated > Payment.Amount)
        {
            ModelState.AddModelError(string.Empty,
                $"Total allocated ({totalAllocated:C}) exceeds payment amount ({Payment.Amount:C}).");
            await LoadSelectListAsync();
            await LoadOutstandingAsync();
            return Page();
        }

        // Update payment scalars
        var existing = await _db.RentPayments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == Payment.Id);
        if (existing is null) return NotFound();

        existing.LeaseId = Payment.LeaseId;
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
