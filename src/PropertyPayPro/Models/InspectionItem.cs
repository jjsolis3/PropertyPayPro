using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public enum InspectionCondition
{
    NotAssessed,
    Excellent,
    Good,
    Fair,
    Poor,
    Damaged,
    Missing
}

/// <summary>
/// One line-item on a room-by-room inspection — "Kitchen / Refrigerator",
/// "Living Room / Walls", etc. On a MoveOut inspection, DeductionAmount
/// and DeductionReason can be populated to record what the tenant is
/// being charged for beyond normal wear.
/// </summary>
public class InspectionItem
{
    public int Id { get; set; }

    public int InspectionId { get; set; }
    public Inspection? Inspection { get; set; }

    [Required, StringLength(80)]
    public string Room { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Item { get; set; } = string.Empty;

    public InspectionCondition Condition { get; set; } = InspectionCondition.NotAssessed;

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Sort order inside a room — 10-step increments so ad-hoc
    /// items inserted later can slot between existing ones.</summary>
    public int Order { get; set; } = 100;

    /// <summary>Move-out only: amount deducted from the security deposit
    /// for this item.</summary>
    [Range(0, 100_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal? DeductionAmount { get; set; }

    [StringLength(500)]
    public string? DeductionReason { get; set; }

    public List<InspectionPhoto> Photos { get; set; } = new();
}
