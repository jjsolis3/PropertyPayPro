using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
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
        if (!ModelState.IsValid) return Page();
        _db.Attach(Charge).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToPage("Details", new { id = Charge.Id });
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
