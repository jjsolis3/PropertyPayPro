using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.ServiceTickets;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DetailsModel(ApplicationDbContext db) => _db = db;

    public ServiceTicket? Ticket { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Ticket = await _db.ServiceTickets
            .Include(t => t.Property)
            .FirstOrDefaultAsync(t => t.Id == id);
        return Ticket is null ? NotFound() : Page();
    }
}
