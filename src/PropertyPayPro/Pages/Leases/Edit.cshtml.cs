using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Leases;

[Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Lease Lease { get; set; } = new();

    public SelectList Properties { get; private set; } = default!;
    public SelectList Tenants { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Leases.FindAsync(id);
        if (entity is null) return NotFound();
        Lease = entity;
        await LoadSelectListsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadSelectListsAsync();
            return Page();
        }

        _db.Attach(Lease).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadSelectListsAsync()
    {
        Properties = new SelectList(await _db.Properties.OrderBy(p => p.Name).ToListAsync(), "Id", "Name");
        var tenants = await _db.Tenants.OrderBy(t => t.LastName).ToListAsync();
        Tenants = new SelectList(tenants.Select(t => new { t.Id, Name = t.DisplayName }), "Id", "Name");
    }
}
