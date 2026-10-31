using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

/// <summary>
/// Append-only audit record for sensitive admin/manager actions.
/// Rows are never updated or deleted at runtime; the table is the
/// paper trail of "who did what, when" across Notices, Leases, Users,
/// and other state changes we care about reviewing later.
/// </summary>
public class AuditEntry
{
    public int Id { get; set; }

    /// <summary>UTC timestamp of the action.</summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Identity user id of the actor. Nullable so we can log actions
    /// taken by the background scheduler (`UserEmail` is set to the
    /// scheduler name in that case).
    /// </summary>
    [StringLength(450)]
    public string? UserId { get; set; }

    /// <summary>Denormalized actor label — email, "scheduler", "system".</summary>
    [StringLength(320)]
    public string? UserEmail { get; set; }

    /// <summary>
    /// Short action code — e.g. "NoticeCreated", "NoticeSent",
    /// "NoticeDeleted", "ScheduledTaskEnqueued". Kept as a string
    /// instead of an enum so new actions don't need a migration.
    /// </summary>
    [Required, StringLength(80)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Entity type the action was taken on, e.g. "LeaseDocument".</summary>
    [StringLength(80)]
    public string? EntityType { get; set; }

    /// <summary>Primary key of the subject entity, when there is one.</summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// Human-readable summary. For sent packages this includes
    /// recipient count + subject; for deletes, the filename; etc.
    /// </summary>
    [StringLength(4000)]
    public string? Details { get; set; }

    /// <summary>Caller IP where available (best-effort).</summary>
    [StringLength(64)]
    public string? IpAddress { get; set; }
}
