using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public enum LeaseDocumentType
{
    Other,
    SignedLease,
    Addendum,
    InsuranceCertificate,
    MoveInChecklist,
    MoveOutInspection,
    Notice,
    RentalApplication,
    BackgroundCheck,
    W9,
    TenantForm,
    PropertyPhoto,
    Correspondence
}

public class LeaseDocument
{
    public int Id { get; set; }

    public int LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public LeaseDocumentType Type { get; set; } = LeaseDocumentType.Other;

    [Required, StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    [Required, StringLength(500)]
    public string StorageKey { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    public DateTime UploadedOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date the document takes effect (e.g. policy start date on an
    /// insurance certificate, effective date of an addendum). Optional.
    /// </summary>
    [DataType(DataType.Date)]
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Date the document expires (e.g. insurance certificate expiration,
    /// background-check validity window). Optional. Documents with a
    /// value here surface on the "expiring soon" dashboard widget.
    /// </summary>
    [DataType(DataType.Date)]
    public DateOnly? ExpiresOn { get; set; }
}
