using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public enum EmailKind
{
    Statement,
    Receipt,
    Reminder,
    Other
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

    public int? LeaseId { get; set; }
    public int? RentPaymentId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
