using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public class Lease
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    [DataType(DataType.Date)]
    public DateOnly StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly EndDate { get; set; }

    [Range(0, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal MonthlyRent { get; set; }

    [Range(0, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal SecurityDeposit { get; set; }

    [Range(1, 31)]
    public int RentDueDay { get; set; } = 1;

    public bool IsMonthToMonth { get; set; }

    [Range(0, 10_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal LateFeeAmount { get; set; }

    [Range(0, 60)]
    public int LateFeeGraceDays { get; set; } = 5;

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<RentPayment> Payments { get; set; } = new();
    public List<RentalCharge> Charges { get; set; } = new();
}
