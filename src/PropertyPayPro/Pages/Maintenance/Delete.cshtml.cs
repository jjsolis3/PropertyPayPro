using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Maintenance;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class DeleteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public DeleteModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public MaintenanceSchedule Schedule { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await _db.MaintenanceSchedules
            .Include(x => x.Property)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        Schedule = s;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var s = await _db.MaintenanceSchedules.FindAsync(id);
        if (s is null) return RedirectToPage("Index");
        var title = s.Title;
        _db.MaintenanceSchedules.Remove(s);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Schedule \"{title}\" deleted.";
        return RedirectToPage("Index");
    }
}
