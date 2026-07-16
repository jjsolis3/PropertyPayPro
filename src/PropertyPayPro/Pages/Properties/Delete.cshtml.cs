using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Properties;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty]
    public Property Property { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var entity = await _db.Properties.FindAsync(id);
        if (entity is null) return NotFound();
        Property = entity;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var entity = await _db.Properties.FindAsync(Property.Id);
        if (entity is null) return RedirectToPage("Index");
        _db.Properties.Remove(entity);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
