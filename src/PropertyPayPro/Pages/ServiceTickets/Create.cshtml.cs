using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.ServiceTickets;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public ServiceTicket Ticket { get; set; } = new();

    public SelectList Properties { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync()
    {
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
        _db.ServiceTickets.Add(Ticket);
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
