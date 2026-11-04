using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Bills;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public RentalCharge Charge { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.RentalCharges.FindAsync(id);
        if (entity is null) return NotFound();
        Charge = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Update only the fields the form edits. Attaching the posted object marked
        // every column modified, and the form has no Kind field, so a late fee was
        // saved back as rent (or hit the unique lease/period/kind index -> 500).
        var entity = await _db.RentalCharges
            .Include(c => c.Allocations)
            .FirstOrDefaultAsync(c => c.Id == Charge.Id);
        if (entity is null) return NotFound();

        var period = new DateOnly(Charge.BillingPeriodStart.Year, Charge.BillingPeriodStart.Month, 1);
        if (ModelState.IsValid)
        {
            var clash = await _db.RentalCharges.AnyAsync(c => c.Id != entity.Id && c.LeaseId == entity.LeaseId
                && c.Kind == entity.Kind && c.BillingPeriodStart == period);
            if (clash)
                ModelState.AddModelError("Charge.BillingPeriodStart",
                    $"This lease already has a {(entity.Kind == ChargeKind.LateFee ? "late fee" : "rent bill")} for {period:MMMM yyyy}.");
            var paid = entity.Allocations.Sum(a => a.Amount);
            if (Charge.AmountDue < paid)
                ModelState.AddModelError("Charge.AmountDue", $"Amount can't be less than what's already been paid ({paid:C}).");
        }
        if (!ModelState.IsValid)
        {
            Charge.Kind = entity.Kind;
            Charge.LeaseId = entity.LeaseId;
            return Page();
        }

        entity.BillingPeriodStart = period;
        entity.DueDate = Charge.DueDate;
        entity.AmountDue = Charge.AmountDue;
        entity.Notes = Charge.Notes;
        await _db.SaveChangesAsync();
        return RedirectToPage("Details", new { id = entity.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var entity = await _db.RentalCharges
            .Include(c => c.Allocations)
            .FirstOrDefaultAsync(c => c.Id == Charge.Id);
        if (entity is null) return RedirectToPage("Index");

        if (entity.Allocations.Any())
        {
            ModelState.AddModelError(string.Empty,
                "Cannot delete a bill with payments allocated to it. Remove the allocations first.");
            Charge = entity;
            return Page();
        }

        _db.RentalCharges.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
