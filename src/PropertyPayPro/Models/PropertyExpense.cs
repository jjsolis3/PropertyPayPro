using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public enum ExpenseCategory
{
    Utility,
    Insurance,
    Tax,
    Mortgage,
    HOA,
    Supplies,
    Misc
}

public class PropertyExpense
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Utility;

    [StringLength(120)]
    public string? Vendor { get; set; }

    [Required, StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal AmountDue { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? PaidOn { get; set; }

    public bool PassThroughToTenant { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [NotMapped]
    public bool IsPaid => PaidOn.HasValue;
}
