using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyPayPro.Models;

public class AppSettings
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }  // always 1 (singleton)

    // Branding
    [Required, StringLength(80)]
    public string AppName { get; set; } = "PropertyPayPro";

    [StringLength(20)]
    public string PrimaryColor { get; set; } = "#1f3a8a";

    [StringLength(20)]
    public string AccentColor { get; set; } = "#d97706";

    [StringLength(500)]
    public string? LogoStorageKey { get; set; }     // overrides /img/brand/PPS_Logo_Main.png

    [StringLength(500)]
    public string? LogoSmallStorageKey { get; set; }  // overrides /img/brand/PPS_Logo_SM.png

    // Email
    [StringLength(160)]
    public string? FromEmailOverride { get; set; }

    [StringLength(80)]
    public string? FromNameOverride { get; set; }

    // Billing defaults
    [Range(1, 31)]
    public int DefaultRentDueDay { get; set; } = 15;

    [Range(0, 60)]
    public int DefaultLateFeeGraceDays { get; set; } = 5;

    [Range(0, 10_000)]
    [Column(TypeName = "numeric(10,2)")]
    public decimal DefaultLateFeeAmount { get; set; } = 0m;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
