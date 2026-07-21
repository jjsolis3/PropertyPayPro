using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Maintenance;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public EditModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public MaintenanceSchedule Schedule { get; set; } = new();
    public SelectList Properties { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await _db.MaintenanceSchedules.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        Schedule = s;
        await LoadPropertiesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadPropertiesAsync();
            return Page();
        }
        _db.Attach(Schedule).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Schedule \"{Schedule.Title}\" updated.";
        return RedirectToPage("Index");
    }

    private async Task LoadPropertiesAsync()
    {
        Properties = new SelectList(
            await _db.Properties.OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
    }
}
