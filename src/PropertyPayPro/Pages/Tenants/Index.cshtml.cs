using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Tenants;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Tenant> Tenants { get; private set; } = new List<Tenant>();

    public async Task OnGetAsync()
    {
        Tenants = await _db.Tenants.OrderBy(t => t.LastName).ToListAsync();
    }
}
