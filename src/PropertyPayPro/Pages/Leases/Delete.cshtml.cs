using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Leases;

[Authorize]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Lease Lease { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenant)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (entity is null) return NotFound();
        Lease = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.Leases.FindAsync(Lease.Id);
        if (entity is null) return RedirectToPage("Index");
        _db.Leases.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
