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

    // Absolute site URL (e.g. https://app.example.com) used to build links and
    // the logo URL in emails sent by background jobs, which have no request
    // to derive the host from.
    [StringLength(200)]
    public string? PublicBaseUrl { get; set; }

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

    // Billing automation, run by the daily billing sweep. All off by default.
    public bool AutoGenerateCharges { get; set; }

    // Day of the month from which the sweep creates that month's rent bills.
    // Capped at 28 so it is reached in every month.
    [Range(1, 28)]
    public int AutoBillingDayOfMonth { get; set; } = 1;

    public bool AutoApplyLateFees { get; set; }
    public bool AutoEmailStatements { get; set; }
    public bool AutoEmailAdminSummary { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
