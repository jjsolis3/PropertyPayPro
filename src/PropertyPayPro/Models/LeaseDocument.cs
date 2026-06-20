using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public class LeaseDocument
{
    public int Id { get; set; }

    public int LeaseId { get; set; }
    public Lease? Lease { get; set; }

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
}
