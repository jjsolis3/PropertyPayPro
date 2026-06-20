using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.ServiceTickets;

[Authorize]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public ServiceTicket Ticket { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.ServiceTickets.FindAsync(id);
        if (entity is null) return NotFound();
        Ticket = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.ServiceTickets.FindAsync(Ticket.Id);
        if (entity is null) return RedirectToPage("Index");
        _db.ServiceTickets.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
