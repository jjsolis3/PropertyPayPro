using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public class PaymentAllocation
{
    public int Id { get; set; }

    public int RentPaymentId { get; set; }
    public RentPayment? Payment { get; set; }

    public int RentalChargeId { get; set; }
    public RentalCharge? RentalCharge { get; set; }

    [Range(0.01, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal Amount { get; set; }
}
