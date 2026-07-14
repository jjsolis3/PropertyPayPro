using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public enum ServiceCategory
{
    HVAC,
    Electrical,
    Plumbing,
    Appliance,
    Landscaping,
    Pest,
    General,
    Other
}

public enum ServiceTicketStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled
}

public enum ServicePriority
{
    Low,
    Normal,
    High,
    Urgent
}

public class ServiceTicket
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public ServiceCategory Category { get; set; } = ServiceCategory.General;
    public ServiceTicketStatus Status { get; set; } = ServiceTicketStatus.Open;
    public ServicePriority Priority { get; set; } = ServicePriority.Normal;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(120)]
    public string? Vendor { get; set; }

    [StringLength(30)]
    public string? VendorPhone { get; set; }

    [DataType(DataType.Date)]
    public DateOnly ReportedOn { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [DataType(DataType.Date)]
    public DateOnly? ScheduledFor { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? ResolvedOn { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? QuotedOn { get; set; }

    [Range(0, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal? QuotedAmount { get; set; }

    [Range(0, 1_000_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal? Cost { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? WarrantyExpiresOn { get; set; }

    public bool PassThroughToTenant { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
