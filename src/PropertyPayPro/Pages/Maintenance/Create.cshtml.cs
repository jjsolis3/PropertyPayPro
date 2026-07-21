using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Maintenance;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public CreateModel(ApplicationDbContext db) => _db = db;

    [BindProperty] public MaintenanceSchedule Schedule { get; set; } = new()
    {
        NextDueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1),
        RecurrenceMonths = 6,
        LeadDays = 14,
        Active = true,
        NotifyTenants = true,
        NotifyAdmins = true
    };

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
        _db.MaintenanceSchedules.Add(Schedule);
        await _db.SaveChangesAsync();
        TempData["Message"] = $"Schedule \"{Schedule.Title}\" created.";
        return RedirectToPage("Index");
    }

    private async Task LoadAsync()
    {
        Properties = new SelectList(
            await _db.Properties.OrderBy(p => p.Name).ToListAsync(),
            "Id", "Name");
    }
}
