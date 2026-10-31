using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;
using PropertyPayPro.Services.ScheduledTasks;

namespace PropertyPayPro.Pages.Settings;

/// <summary>
/// Admin dashboard for the background scheduler. Shows recurring jobs
/// (pending/running/last-run), lets admins run them now, and lists
/// recent task history for diagnostics.
/// </summary>
public class SchedulerModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLog _audit;

    public SchedulerModel(ApplicationDbContext db, IAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public record Row(
        ScheduledTaskKind Kind,
        ScheduledTask? Next,
        ScheduledTask? LastCompleted);

    public List<Row> Recurring { get; private set; } = new();
    public List<ScheduledTask> Recent { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["SettingsTab"] = "scheduler";

        var kinds = new[]
        {
            ScheduledTaskKind.LateRentReminder,
            ScheduledTaskKind.LeaseRenewalReminder,
            ScheduledTaskKind.InspectionReminder,
        };
        foreach (var kind in kinds)
        {
            var next = await _db.ScheduledTasks
                .Where(t => t.Kind == kind
                    && (t.Status == ScheduledTaskStatus.Pending
                        || t.Status == ScheduledTaskStatus.Running))
                .OrderBy(t => t.RunAtUtc)
                .FirstOrDefaultAsync();
            var last = await _db.ScheduledTasks
                .Where(t => t.Kind == kind
                    && (t.Status == ScheduledTaskStatus.Completed
                        || t.Status == ScheduledTaskStatus.Failed))
                .OrderByDescending(t => t.CompletedUtc)
                .FirstOrDefaultAsync();
            Recurring.Add(new Row(kind, next, last));
        }

        Recent = await _db.ScheduledTasks
            .OrderByDescending(t => t.CreatedUtc)
            .Take(50)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostRunNowAsync(int kind)
    {
        if (!Enum.IsDefined(typeof(ScheduledTaskKind), kind))
            return BadRequest();
        var k = (ScheduledTaskKind)kind;

        // Nudge the next Pending row to run immediately. If none exists
        // (handler failed hard before re-enqueueing), enqueue a fresh one.
        var now = DateTime.UtcNow;
        var next = await _db.ScheduledTasks
            .Where(t => t.Kind == k && t.Status == ScheduledTaskStatus.Pending)
            .OrderBy(t => t.RunAtUtc)
            .FirstOrDefaultAsync();
        if (next is not null)
        {
            next.RunAtUtc = now;
        }
        else
        {
            _db.ScheduledTasks.Add(new ScheduledTask
            {
                Kind = k,
                Status = ScheduledTaskStatus.Pending,
                RunAtUtc = now,
                PayloadJson = "{}",
                Description = $"Admin-triggered {k} run",
                CreatedUtc = now,
            });
        }
        await _db.SaveChangesAsync();
        await _audit.WriteAsync("ScheduledTaskNudged",
            entityType: nameof(ScheduledTask), entityId: next?.Id,
            details: $"Kind={k}; now runnable");

        TempData["Message"] = $"{k} will run on the next scheduler tick (within ~60s).";
        return RedirectToPage();
    }
}
