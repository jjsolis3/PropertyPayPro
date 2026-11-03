using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Helpers shared by the recurring reminder handlers. Keeps the
/// "re-enqueue myself for tomorrow" bookkeeping in one place.
/// </summary>
internal static class RecurringTaskHelpers
{
    /// <summary>Hour, in the app's time zone, that recurring daily jobs fire.</summary>
    public const int DailyRunHourLocal = 7;

    /// <summary>Recurring job kinds, as opposed to one-off scheduled sends.</summary>
    public static readonly ScheduledTaskKind[] RecurringKinds =
    {
        ScheduledTaskKind.LateRentReminder,
        ScheduledTaskKind.LeaseRenewalReminder,
        ScheduledTaskKind.InspectionReminder,
        ScheduledTaskKind.BillingSweep,
    };

    /// <summary>Next 7:00 AM in the app's time zone strictly in the future, as UTC.</summary>
    public static DateTime NextDailyRunUtc(DateTime nowUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZoneInfo.Local);
        var target = local.Date.AddHours(DailyRunHourLocal);
        if (target <= local) target = target.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(target, DateTimeKind.Unspecified), TimeZoneInfo.Local);
    }

    /// <summary>
    /// Queue the next run of a daily recurring job, UNLESS one is
    /// already in the queue for this kind. The handler calls this in
    /// its finally so a retriable failure (which replays the handler)
    /// doesn't pile up duplicate next-day rows — a single Pending or
    /// Running row for the same kind means we're already covered.
    /// </summary>
    public static void EnqueueNextRun(
        ApplicationDbContext db,
        ScheduledTaskKind kind,
        string description)
    {
        // Pending-only. The currently-executing task is Running and
        // doesn't block the next-day enqueue; a Pending row (one we
        // added on a prior retry, or the bootstrap enqueued at
        // startup) does.
        var hasPending = db.ScheduledTasks.Local.Any(t =>
                t.Kind == kind && t.Status == ScheduledTaskStatus.Pending)
            || db.ScheduledTasks.Any(t =>
                t.Kind == kind && t.Status == ScheduledTaskStatus.Pending);
        if (hasPending) return;

        db.ScheduledTasks.Add(new ScheduledTask
        {
            Kind = kind,
            Status = ScheduledTaskStatus.Pending,
            RunAtUtc = NextDailyRunUtc(DateTime.UtcNow),
            PayloadJson = "{}",
            Description = description,
            CreatedUtc = DateTime.UtcNow,
        });
    }
}
