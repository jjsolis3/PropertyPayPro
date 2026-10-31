using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public enum EmailKind
{
    Statement,
    Receipt,
    Reminder,
    ReimbursementReminder,
    Other,
    Invite,
    Notice,
    Broadcast,
    MaintenanceReminder,
    // Appended for PR B. New values must be appended because the enum
    // is stored as its underlying int in EmailLog.Kind — reordering
    // would silently remap existing rows.
    LateRentReminder,
    LeaseRenewalReminder,
    InspectionReminder,
}

public enum EmailStatus
{
    Sent,
    Failed
}

public class EmailLog
{
    public int Id { get; set; }

    public EmailKind Kind { get; set; }
    public EmailStatus Status { get; set; }

    [Required, StringLength(320)]
    public string ToAddress { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Error { get; set; }

    /// <summary>
    /// The rendered HTML body that was (or was attempted to be) sent.
    /// Stored so admins can resend a failed delivery or audit the
    /// exact message content later. Not stored for very large bodies
    /// (the sender truncates past the column limit).
    /// </summary>
    public string? BodyHtml { get; set; }

    public int? LeaseId { get; set; }
    public int? RentPaymentId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
