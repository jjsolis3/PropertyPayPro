using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public RentalCharge? Charge { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Charge = await _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenant)
            .Include(c => c.Allocations).ThenInclude(a => a.Payment)
            .FirstOrDefaultAsync(c => c.Id == id);
        return Charge is null ? NotFound() : Page();
    }
}
