using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Leases;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<Lease> Leases { get; private set; } = new List<Lease>();

    public async Task OnGetAsync()
    {
        Leases = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();
    }
}
