using System.ComponentModel.DataAnnotations;

namespace PropertyPayPro.Models;

public enum NoticeTemplateScope
{
    /// <summary>The body text of a generated notice PDF (/Notices/Create).</summary>
    PdfBody = 0,

    /// <summary>The cover email that delivers one or more PDFs (/Notices/Send).</summary>
    CoverEmail = 1,

    /// <summary>Email body used by a recurring reminder handler.</summary>
    SystemReminder = 2,
}

/// <summary>
/// Admin-editable template used across the Notices workflow and the
/// recurring reminder handlers. Rows are seeded on first startup with
/// the previously hard-coded catalog contents; the admin can edit the
/// Title/Subject and Body text in /Settings/Templates. Deleting a
/// system row is blocked (<see cref="IsSystem"/>) — the admin can
/// reset such a row to its seeded default instead.
///
/// Tokens are resolved at render time by
/// <c>NoticeTemplateService.Render</c>. Supported tokens include
/// <c>{{PropertyName}}</c>, <c>{{Address}}</c>, <c>{{TenantNames}}</c>,
/// <c>{{MonthlyRent}}</c>, <c>{{OutstandingAmount}}</c>,
/// <c>{{LeaseStart}}</c>, <c>{{LeaseEnd}}</c>, <c>{{TodayDate}}</c>,
/// <c>{{NoticeTitle}}</c>, <c>{{InspectionDate}}</c>,
/// <c>{{InspectionKind}}</c>.
/// </summary>
public class NoticeTemplate
{
    public int Id { get; set; }

    public NoticeTemplateScope Scope { get; set; }

    /// <summary>
    /// Short stable key, unique per <see cref="Scope"/>. Code lookups
    /// (e.g. the recurring handlers) go by Slug, not Id, so a reseed
    /// or restore survives a template id change.
    /// </summary>
    [Required, StringLength(64)]
    public string Slug { get; set; } = "";

    [Required, StringLength(120)]
    public string Label { get; set; } = "";

    /// <summary>PDF title for PdfBody rows, email subject for others.</summary>
    [StringLength(300)]
    public string? TitleOrSubject { get; set; }

    [Required]
    public string Body { get; set; } = "";

    /// <summary>Rows marked IsSystem can be edited but not deleted.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
