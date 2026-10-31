using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Helpers shared by the recurring reminder handlers. Keeps the
/// "re-enqueue myself for tomorrow" bookkeeping in one place.
/// </summary>
internal static class RecurringTaskHelpers
{
    /// <summary>
    /// Fixed time-of-day in UTC that recurring daily jobs fire.
    /// 15:00 UTC ≈ 10:00 Eastern / 07:00 Pacific — early enough to be
    /// in the mailbox by the start of the business day in US timezones.
    /// </summary>
    public const int DailyRunHourUtc = 15;

    /// <summary>Next 15:00 UTC strictly in the future.</summary>
    public static DateTime NextDailyRunUtc(DateTime nowUtc)
    {
        var target = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day,
            DailyRunHourUtc, 0, 0, DateTimeKind.Utc);
        if (target <= nowUtc) target = target.AddDays(1);
        return target;
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
