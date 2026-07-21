using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public enum InspectionKind
{
    MoveIn,
    MoveOut
}

public enum InspectionStatus
{
    Draft,
    Completed
}

/// <summary>
/// Room-by-room condition inspection at move-in or move-out. Serves as
/// the baseline for security-deposit deductions and as evidence of
/// pre-existing damage. On completion, a PDF report is generated and
/// attached to the lease as a Notice-adjacent LeaseDocument (type
/// MoveInChecklist or MoveOutInspection — the enum values that were
/// already added in PR #50).
/// </summary>
public class Inspection
{
    public int Id { get; set; }

    public int LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public InspectionKind Kind { get; set; }
    public InspectionStatus Status { get; set; } = InspectionStatus.Draft;

    [DataType(DataType.Date)]
    public DateOnly ScheduledFor { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? CompletedOn { get; set; }

    [StringLength(120)]
    public string? ConductedBy { get; set; }

    public bool TenantPresent { get; set; }

    [StringLength(2000)]
    public string? OverallNotes { get; set; }

    /// <summary>
    /// Set on a MoveOut inspection to point back at the paired MoveIn
    /// it is being compared against. Optional — an admin can create a
    /// stand-alone MoveOut too.
    /// </summary>
    public int? PairedMoveInId { get; set; }
    public Inspection? PairedMoveIn { get; set; }

    /// <summary>
    /// Id of the LeaseDocument row that stores the generated PDF, if the
    /// inspection has been finalized. Lets the UI link straight to the
    /// preview from the inspection detail page.
    /// </summary>
    public int? GeneratedDocumentId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<InspectionItem> Items { get; set; } = new();

    [NotMapped]
    public decimal TotalDeductions => Items.Sum(i => i.DeductionAmount ?? 0m);
}
