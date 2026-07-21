using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

/// <summary>
/// One photo attached to an InspectionItem. Bytes live in IDocumentStorage
/// under inspections/{inspectionId}/; only metadata is stored in the DB.
/// </summary>
public class InspectionPhoto
{
    public int Id { get; set; }

    public int InspectionItemId { get; set; }
    public InspectionItem? InspectionItem { get; set; }

    [Required, StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    [Required, StringLength(500)]
    public string StorageKey { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Caption { get; set; }

    public DateTime UploadedOn { get; set; } = DateTime.UtcNow;
}
