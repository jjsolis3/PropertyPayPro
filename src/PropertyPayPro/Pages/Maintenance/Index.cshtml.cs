using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Maintenance;

/// <summary>
/// Lists every preventive-maintenance schedule with its next-due
/// status. Runs the scheduler opportunistically on GET so a lightweight
/// visit to this page is enough to trigger generation of any tickets
/// that came due since the last time the page (or the /api/jobs
/// endpoint) was hit. Admins can also force a run via the button.
/// </summary>
[Authorize(Roles = IdentitySeed.AdminRole + "," + IdentitySeed.ManagerRole)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MaintenanceSchedulerService _scheduler;

    public IndexModel(ApplicationDbContext db, MaintenanceSchedulerService scheduler)
    {
        _db = db;
        _scheduler = scheduler;
    }

    public List<MaintenanceSchedule> Schedules { get; private set; } = new();
    public int ActiveCount { get; private set; }
    public int DueSoonCount { get; private set; }
    public int PausedCount { get; private set; }

    public async Task OnGetAsync()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        // Opportunistic run — cheap when nothing is due, and keeps
        // schedules moving even if the external cron isn't wired up.
        await _scheduler.GenerateDueTicketsAsync(baseUrl);
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostGenerateNowAsync()
    {
        if (!User.IsInRole(IdentitySeed.AdminRole)) return Forbid();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = await _scheduler.GenerateDueTicketsAsync(baseUrl);
        TempData["Message"] = result.TicketsCreated == 0
            ? $"Checked {result.SchedulesEvaluated} schedule(s). Nothing was due."
            : $"Generated {result.TicketsCreated} ticket(s), sent {result.NotificationsSent} notification(s).";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Schedules = await _db.MaintenanceSchedules
            .Include(s => s.Property)
            .OrderBy(s => s.Active ? 0 : 1)
            .ThenBy(s => s.NextDueDate)
            .ToListAsync();
        ActiveCount = Schedules.Count(s => s.Active);
        PausedCount = Schedules.Count(s => !s.Active);
        DueSoonCount = Schedules.Count(s => s.Active && s.DaysUntilDue <= s.LeadDays);
    }
}
