using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Tenants;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Tenant? Tenant { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Tenant = await _db.Tenants.FindAsync(id);
        return Tenant is null ? NotFound() : Page();
    }
}
