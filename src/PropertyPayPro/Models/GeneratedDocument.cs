using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public enum GeneratedDocumentKind
{
    Bill,
    Receipt,
    UnpaidStatement,
    BillPaidConfirmation
}

public class GeneratedDocument
{
    public int Id { get; set; }

    public GeneratedDocumentKind Kind { get; set; }

    public int? LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public int? RentalChargeId { get; set; }
    public RentalCharge? RentalCharge { get; set; }

    public int? RentPaymentId { get; set; }
    public RentPayment? RentPayment { get; set; }

    [Required, StringLength(500)]
    public string StorageKey { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
