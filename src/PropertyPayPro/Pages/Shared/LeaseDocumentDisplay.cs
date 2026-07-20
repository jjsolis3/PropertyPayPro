using PropertyPayPro.Models;

namespace PropertyPayPro.Pages.Shared;

/// <summary>
/// Presentation helpers for LeaseDocument. Shared by the admin Leases/
/// Details Documents tab, /Documents/Index, /Portal/Documents, and the
/// dashboard "expiring documents" widget so labels and expiration
/// styling stay consistent across every surface.
/// </summary>
public static class LeaseDocumentDisplay
{
    public static string TypeLabel(LeaseDocumentType type) => type switch
    {
        LeaseDocumentType.SignedLease => "Signed Lease",
        LeaseDocumentType.Addendum => "Addendum",
        LeaseDocumentType.InsuranceCertificate => "Insurance Certificate",
        LeaseDocumentType.MoveInChecklist => "Move-in Checklist",
        LeaseDocumentType.MoveOutInspection => "Move-out Inspection",
        LeaseDocumentType.Notice => "Notice",
        LeaseDocumentType.RentalApplication => "Rental Application",
        LeaseDocumentType.BackgroundCheck => "Background Check",
        LeaseDocumentType.W9 => "W-9",
        LeaseDocumentType.TenantForm => "Tenant Form",
        LeaseDocumentType.PropertyPhoto => "Property Photo",
        LeaseDocumentType.Correspondence => "Correspondence",
        _ => "Other"
    };

    public static string TypeBadgeColor(LeaseDocumentType type) => type switch
    {
        LeaseDocumentType.SignedLease => "primary",
        LeaseDocumentType.Addendum => "info",
        LeaseDocumentType.InsuranceCertificate => "warning",
        LeaseDocumentType.MoveInChecklist => "success",
        LeaseDocumentType.MoveOutInspection => "success",
        LeaseDocumentType.Notice => "danger",
        LeaseDocumentType.RentalApplication => "info",
        LeaseDocumentType.BackgroundCheck => "warning",
        LeaseDocumentType.W9 => "secondary",
        LeaseDocumentType.TenantForm => "secondary",
        LeaseDocumentType.PropertyPhoto => "info",
        LeaseDocumentType.Correspondence => "secondary",
        _ => "secondary"
    };

    public enum ExpirationStatus { NoExpiry, Expired, ExpiringSoon, Ok }

    public static ExpirationStatus Status(DateOnly? expiresOn, int expiringSoonDays = 30)
    {
        if (!expiresOn.HasValue) return ExpirationStatus.NoExpiry;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var days = expiresOn.Value.DayNumber - today.DayNumber;
        if (days < 0) return ExpirationStatus.Expired;
        if (days <= expiringSoonDays) return ExpirationStatus.ExpiringSoon;
        return ExpirationStatus.Ok;
    }

    public static (string label, string color) StatusBadge(DateOnly? expiresOn, int expiringSoonDays = 30)
    {
        if (!expiresOn.HasValue) return ("", "");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var days = expiresOn.Value.DayNumber - today.DayNumber;
        if (days < 0) return ($"Expired {-days}d ago", "danger");
        if (days == 0) return ("Expires today", "danger");
        if (days <= expiringSoonDays) return ($"Expires in {days}d", "warning");
        return ($"Expires {expiresOn.Value:yyyy-MM-dd}", "success");
    }

    /// <summary>
    /// Which content types get inline preview vs a plain download link.
    /// PDFs render in an iframe; images render in an img tag.
    /// </summary>
    public static bool IsPreviewable(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;
        return contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImage(string? contentType) =>
        !string.IsNullOrEmpty(contentType)
        && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    public static bool IsPdf(string? contentType) =>
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
}
