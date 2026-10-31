using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

/// <summary>
/// Seeds the NoticeTemplates table with the formerly hard-coded
/// catalog contents on first start. Idempotent per-slug — if a system
/// row already exists it is left alone (so admin edits are preserved
/// across restarts).
///
/// To add a new system template, add an entry to <see cref="Defaults"/>
/// and bump the deploy. To change a shipped default without touching
/// production edits, change <c>Defaults</c> and ship a "reset to
/// default" from the admin UI.
/// </summary>
public class NoticeTemplateSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NoticeTemplateSeeder> _logger;

    public NoticeTemplateSeeder(ApplicationDbContext db, ILogger<NoticeTemplateSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existingSlugs = await _db.NoticeTemplates
            .Select(t => new { t.Scope, t.Slug })
            .ToListAsync(ct);
        var have = existingSlugs
            .Select(x => (x.Scope, x.Slug))
            .ToHashSet();

        var inserted = 0;
        foreach (var def in Defaults)
        {
            if (have.Contains((def.Scope, def.Slug))) continue;
            _db.NoticeTemplates.Add(new NoticeTemplate
            {
                Scope = def.Scope,
                Slug = def.Slug,
                Label = def.Label,
                TitleOrSubject = def.TitleOrSubject,
                Body = def.Body,
                IsSystem = true,
                IsActive = true,
                SortOrder = def.Sort,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });
            inserted++;
        }
        if (inserted > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seeded {Count} notice templates", inserted);
        }
    }

    /// <summary>
    /// Resets a system row to its seeded default. Returns true if the
    /// row was reset. Non-system rows cannot be reset (there is no
    /// baked default).
    /// </summary>
    public async Task<bool> ResetToDefaultAsync(int templateId, CancellationToken ct = default)
    {
        var row = await _db.NoticeTemplates.FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (row is null || !row.IsSystem) return false;
        var def = Defaults.FirstOrDefault(d => d.Scope == row.Scope && d.Slug == row.Slug);
        if (def is null) return false;
        row.Label = def.Label;
        row.TitleOrSubject = def.TitleOrSubject;
        row.Body = def.Body;
        row.SortOrder = def.Sort;
        row.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public record Default(
        NoticeTemplateScope Scope,
        string Slug,
        string Label,
        string? TitleOrSubject,
        string Body,
        int Sort = 100);

    /// <summary>Well-known slugs the recurring reminder handlers look up.</summary>
    public static class Slugs
    {
        public const string LateRentReminderEmail = "late-rent-reminder";
        public const string LeaseRenewalReminderEmail = "lease-renewal-reminder";
        public const string InspectionReminderEmail = "inspection-reminder";
    }

    public static IReadOnlyList<Default> Defaults { get; } = new[]
    {
        // --------- PDF body templates (/Notices/Create) ---------
        new Default(NoticeTemplateScope.PdfBody, "custom", "Custom notice",
            "NOTICE",
            "TO: {{TenantNames}}\nRE: {{Address}}\n\n[Enter your notice text here.]\n\nDated: {{TodayDate}}",
            10),
        new Default(NoticeTemplateScope.PdfBody, "pay-or-quit", "Pay or Quit",
            "NOTICE TO PAY RENT OR QUIT",
@"TO: {{TenantNames}}
RE: {{Address}}

You are hereby notified that rent in the amount of {{OutstandingAmount}} is now due and unpaid for the premises you occupy at the address above.

You must pay the full amount owed WITHIN THREE (3) DAYS from receipt of this notice or vacate and surrender possession of the premises. Failure to comply will result in the initiation of unlawful detainer proceedings.

If you have already paid, please disregard this notice.

Dated: {{TodayDate}}", 20),
        new Default(NoticeTemplateScope.PdfBody, "non-renewal", "Notice of Non-Renewal",
            "NOTICE OF NON-RENEWAL OF LEASE",
@"TO: {{TenantNames}}
RE: {{Address}}

Please be advised that your lease at the above premises will not be renewed and will terminate on {{LeaseEnd}}.

You are required to vacate and return possession of the premises on or before that date. Please contact us to schedule a move-out inspection at your earliest convenience.

Dated: {{TodayDate}}", 30),
        new Default(NoticeTemplateScope.PdfBody, "rent-increase", "Rent Increase",
            "NOTICE OF RENT INCREASE",
@"TO: {{TenantNames}}
RE: {{Address}}

Please be advised that effective on the first rent due date at least 30 days from receipt of this notice, the monthly rent for the above premises will be adjusted.

Current monthly rent: {{MonthlyRent}}
New monthly rent: [ENTER NEW AMOUNT]
Effective date: [ENTER EFFECTIVE DATE]

All other terms of your lease remain unchanged.

Dated: {{TodayDate}}", 40),
        new Default(NoticeTemplateScope.PdfBody, "lease-violation", "Lease Violation",
            "NOTICE OF LEASE VIOLATION",
@"TO: {{TenantNames}}
RE: {{Address}}

This is to notify you of the following violation of your lease agreement:

[DESCRIBE VIOLATION]

You are required to cure the violation within [NUMBER] days of receipt of this notice. Failure to do so may result in termination of your tenancy.

Dated: {{TodayDate}}", 50),
        new Default(NoticeTemplateScope.PdfBody, "entry-notice", "24-Hour Entry Notice",
            "24-HOUR NOTICE OF INTENT TO ENTER",
@"TO: {{TenantNames}}
RE: {{Address}}

Pursuant to applicable law, this is written notice that the landlord or their agent intends to enter the premises for the following purpose:

Date of entry: [ENTER DATE]
Approximate time: [ENTER TIME WINDOW]
Reason: [e.g. routine inspection, repairs, showing to prospective tenant]

If the above date/time is inconvenient, please contact us to reschedule.

Dated: {{TodayDate}}", 60),
        new Default(NoticeTemplateScope.PdfBody, "late-rent-reminder-pdf", "Late Rent Reminder (PDF)",
            "REMINDER: RENT PAST DUE",
@"TO: {{TenantNames}}
RE: {{Address}}

Our records show an outstanding balance of {{OutstandingAmount}} on your account. Rent is now past due.

Please remit payment as soon as possible to avoid additional late fees. If you've already paid, thank you — please disregard this reminder.

Dated: {{TodayDate}}", 70),

        // --------- Cover email templates (/Notices/Send) ---------
        new Default(NoticeTemplateScope.CoverEmail, "custom", "Custom cover email",
            "{{NoticeTitle}}",
@"Dear {{TenantNames}},

[Enter your cover message here.]

Thank you,
Property Management", 10),
        new Default(NoticeTemplateScope.CoverEmail, "formal-delivery", "Formal notice delivery",
            "Formal notice regarding {{PropertyName}}",
@"Dear {{TenantNames}},

Please find attached a formal notice regarding the premises at {{Address}}.

Review the attached document carefully. If you have questions or
believe you have received it in error, contact us as soon as possible.

Thank you,
Property Management", 20),
        new Default(NoticeTemplateScope.CoverEmail, "rent-increase-delivery", "Rent increase delivery",
            "Notice of rent increase — {{PropertyName}}",
@"Dear {{TenantNames}},

Please find attached formal notice of a rent adjustment on your lease
at {{Address}}, along with any supporting documents.

The attachment(s) describe the new rent amount and effective date.
If you have questions, please reach out before the effective date.

Thank you,
Property Management", 30),
        new Default(NoticeTemplateScope.CoverEmail, "lease-renewal-delivery", "Lease renewal package",
            "Lease renewal — {{PropertyName}}",
@"Dear {{TenantNames}},

We're pleased to offer a renewal of your lease at {{Address}}. Please
find the renewal documents attached for your review.

Review the attachments carefully, let us know if you have any
questions, and return the signed documents by the date noted on the
renewal.

Thank you,
Property Management", 40),
        new Default(NoticeTemplateScope.CoverEmail, "pay-or-quit-delivery", "Pay-or-quit delivery",
            "IMPORTANT: Notice to pay rent or quit — {{PropertyName}}",
@"Dear {{TenantNames}},

A formal Notice to Pay Rent or Quit for the premises at {{Address}} is
attached to this email.

The notice specifies the amount owed and the time period in which to
cure. Failure to comply may result in the initiation of unlawful
detainer proceedings. If payment has already been remitted, please
disregard.

Property Management", 50),
        new Default(NoticeTemplateScope.CoverEmail, "general-correspondence", "General correspondence",
            "{{NoticeTitle}}",
@"Dear {{TenantNames}},

Please see the attached regarding your lease at {{Address}}.

Thank you,
Property Management", 60),

        // --------- Recurring reminder emails ---------
        new Default(NoticeTemplateScope.SystemReminder, Slugs.LateRentReminderEmail, "Late rent reminder (email)",
            "Rent past due — {{PropertyName}}",
@"Dear {{TenantNames}},

Our records show an outstanding balance of {{OutstandingAmount}} on
your lease at {{Address}}.

Please remit payment at your earliest convenience to avoid additional
late fees. If you have already paid, thank you — please disregard this
reminder.

Thank you,
Property Management", 10),
        new Default(NoticeTemplateScope.SystemReminder, Slugs.LeaseRenewalReminderEmail, "Lease renewal reminder (email)",
            "Your lease at {{PropertyName}} is ending soon",
@"Dear {{TenantNames}},

Your lease at {{Address}} is scheduled to end on {{LeaseEnd}}.

We'd like to discuss your plans before then — whether you intend to
renew, go month-to-month, or move out. Please reach out at your
earliest convenience.

Thank you,
Property Management", 20),
        new Default(NoticeTemplateScope.SystemReminder, Slugs.InspectionReminderEmail, "Inspection reminder (email)",
            "Upcoming inspection — {{PropertyName}}",
@"Dear {{TenantNames}},

This is a reminder that a {{InspectionKind}} inspection is scheduled
at {{Address}} on {{InspectionDate}}.

If you have questions or need to reschedule, please contact us as soon
as possible.

Thank you,
Property Management", 30),
    };
}
