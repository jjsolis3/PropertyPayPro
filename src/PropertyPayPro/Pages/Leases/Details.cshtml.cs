using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Leases;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Lease? Lease { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Lease = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .Include(l => l.Payments)
            .FirstOrDefaultAsync(l => l.Id == id);
        return Lease is null ? NotFound() : Page();
    }
}
