using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Called at startup to make sure every recurring job kind has at
/// least one Pending task row in the queue. If a handler failed hard
/// and never re-enqueued itself, this is the belt-and-suspenders
/// recovery. Idempotent — a kind with any Pending or Running row is
/// left alone.
/// </summary>
public static class RecurringJobBootstrap
{
    private static readonly (ScheduledTaskKind Kind, string Description)[] Recurring =
    {
        (ScheduledTaskKind.LateRentReminder, "Daily late-rent reminder sweep"),
        (ScheduledTaskKind.LeaseRenewalReminder, "Daily lease-renewal reminder sweep"),
        (ScheduledTaskKind.InspectionReminder, "Daily inspection reminder sweep"),
        (ScheduledTaskKind.BillingSweep, "Daily billing sweep"),
    };

    public static async Task EnsureEnqueuedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        foreach (var (kind, description) in Recurring)
        {
            var alreadyQueued = await db.ScheduledTasks.AnyAsync(t =>
                t.Kind == kind
                && (t.Status == ScheduledTaskStatus.Pending
                    || t.Status == ScheduledTaskStatus.Running), ct);
            if (alreadyQueued) continue;

            db.ScheduledTasks.Add(new ScheduledTask
            {
                Kind = kind,
                Status = ScheduledTaskStatus.Pending,
                RunAtUtc = RecurringTaskHelpers.NextDailyRunUtc(DateTime.UtcNow),
                PayloadJson = "{}",
                Description = description,
                CreatedUtc = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
