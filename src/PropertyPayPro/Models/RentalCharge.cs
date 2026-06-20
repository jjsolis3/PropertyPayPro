using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public enum ChargeStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
    Overdue
}

public class RentalCharge
{
    public int Id { get; set; }

    public int LeaseId { get; set; }
    public Lease? Lease { get; set; }

    [DataType(DataType.Date)]
    public DateOnly BillingPeriodStart { get; set; }

    [DataType(DataType.Date)]
    public DateOnly DueDate { get; set; }

    [Range(0, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal AmountDue { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<PaymentAllocation> Allocations { get; set; } = new();

    [NotMapped]
    public decimal AmountPaid => Allocations?.Sum(a => a.Amount) ?? 0m;

    [NotMapped]
    public decimal Balance => AmountDue - AmountPaid;

    [NotMapped]
    public ChargeStatus Status
    {
        get
        {
            if (AmountPaid >= AmountDue) return ChargeStatus.Paid;
            if (AmountPaid > 0) return ChargeStatus.PartiallyPaid;
            if (DueDate < DateOnly.FromDateTime(DateTime.UtcNow)) return ChargeStatus.Overdue;
            return ChargeStatus.Unpaid;
        }
    }

    [NotMapped]
    public string PeriodLabel => BillingPeriodStart.ToString("MMMM yyyy");
}
