using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Properties;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DetailsModel(ApplicationDbContext db) => _db = db;

    public Property? Property { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Property = await _db.Properties.FindAsync(id);
        return Property is null ? NotFound() : Page();
    }
}
