using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.ServiceTickets;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public ServiceTicket Ticket { get; set; } = new();

    public SelectList Properties { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.ServiceTickets.FindAsync(id);
        if (entity is null) return NotFound();
        Ticket = entity;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        _db.Attach(Ticket).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadAsync()
    {
        Properties = new SelectList(
            await _db.Properties.OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
    }
}
