using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public enum ScheduledTaskKind
{
    /// <summary>Deliver a pre-composed notice package at a future time.</summary>
    NoticeSend = 0,

    /// <summary>Daily sweep for charges past their grace period.</summary>
    LateRentReminder = 1,

    /// <summary>Daily sweep for leases ending in N days.</summary>
    LeaseRenewalReminder = 2,

    /// <summary>Daily sweep for upcoming inspections.</summary>
    InspectionReminder = 3,
}

public enum ScheduledTaskStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
}

/// <summary>
/// A unit of deferred work. The <see cref="ScheduledTaskRunner"/>
/// hosted service polls this table and dispatches due rows to a
/// handler registered by <see cref="Kind"/>.
///
/// Keeping work in the database (vs. an in-process timer) means a
/// pod restart doesn't lose scheduled sends and we can see what's
/// queued from the admin UI.
/// </summary>
public class ScheduledTask
{
    public int Id { get; set; }

    public ScheduledTaskKind Kind { get; set; }
    public ScheduledTaskStatus Status { get; set; } = ScheduledTaskStatus.Pending;

    /// <summary>When the task becomes eligible to run.</summary>
    public DateTime RunAtUtc { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }

    /// <summary>JSON blob the handler for Kind knows how to deserialize.</summary>
    public string PayloadJson { get; set; } = "{}";

    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 3;

    [StringLength(4000)]
    public string? LastError { get; set; }

    /// <summary>Who enqueued it (null for recurring system jobs).</summary>
    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    /// <summary>Short, human-readable summary for the admin list.</summary>
    [StringLength(300)]
    public string? Description { get; set; }
}
