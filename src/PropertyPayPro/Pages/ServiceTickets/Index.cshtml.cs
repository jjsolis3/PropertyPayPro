using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.ServiceTickets;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) => _db = db;

    public IList<ServiceTicket> Tickets { get; private set; } = new List<ServiceTicket>();

    public async Task OnGetAsync()
    {
        Tickets = await _db.ServiceTickets
            .Include(t => t.Property)
            .OrderByDescending(t => t.ReportedOn)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }
}
