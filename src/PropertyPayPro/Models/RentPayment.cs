using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public enum PaymentMethod
{
    Cash,
    Check,
    BankTransfer,
    Card,
    Other
}

public class RentPayment
{
    public int Id { get; set; }

    public int LeaseId { get; set; }
    public Lease? Lease { get; set; }

    [DataType(DataType.Date)]
    public DateOnly PaidOn { get; set; }

    [Range(0, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;

    [StringLength(80)]
    public string? Reference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<PaymentAllocation> Allocations { get; set; } = new();

    [NotMapped]
    public decimal AllocatedAmount => Allocations?.Sum(a => a.Amount) ?? 0m;

    [NotMapped]
    public decimal UnallocatedAmount => Amount - AllocatedAmount;
}
