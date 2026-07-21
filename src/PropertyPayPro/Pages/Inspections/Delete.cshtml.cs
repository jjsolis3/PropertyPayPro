using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Inspections;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;
    public DeleteModel(ApplicationDbContext db, IDocumentStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [BindProperty] public Inspection Inspection { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var i = await _db.Inspections
            .Include(x => x.Lease).ThenInclude(l => l!.Property)
            .Include(x => x.Items).ThenInclude(it => it.Photos)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound();
        Inspection = i;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var i = await _db.Inspections
            .Include(x => x.Items).ThenInclude(it => it.Photos)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return RedirectToPage("Index");

        // Best-effort blob cleanup — DB-level cascade takes care of the
        // metadata rows.
        foreach (var photo in i.Items.SelectMany(it => it.Photos))
        {
            try { await _storage.DeleteAsync(photo.StorageKey); } catch { /* ignore */ }
        }

        _db.Inspections.Remove(i);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Inspection #{id} deleted.";
        return RedirectToPage("Index");
    }
}
