using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Tenants;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Tenant Tenant { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Tenants.FindAsync(id);
        if (entity is null) return NotFound();
        Tenant = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.Tenants.FindAsync(Tenant.Id);
        if (entity is null) return RedirectToPage("Index");
        _db.Tenants.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
