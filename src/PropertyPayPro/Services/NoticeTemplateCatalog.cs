using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

public enum NoticeTemplateKind
{
    Custom,
    PayOrQuit,
    NonRenewal,
    RentIncrease,
    LeaseViolation,
    EntryNotice,
    LateRentReminder
}

public record NoticeTemplate(
    NoticeTemplateKind Kind,
    string Label,
    string DefaultTitle,
    string DefaultBody);

/// <summary>
/// In-memory catalog of notice templates. Each template provides a
/// default title and body seeded from lease/property/tenant context —
/// the admin edits both freely in the UI before generating the PDF.
/// Not persisted; adding a new kind is a code change (rarely needed).
/// </summary>
public static class NoticeTemplateCatalog
{
    public static IReadOnlyList<(NoticeTemplateKind Kind, string Label)> Kinds { get; } = new[]
    {
        (NoticeTemplateKind.Custom, "Custom notice"),
        (NoticeTemplateKind.PayOrQuit, "Pay or Quit"),
        (NoticeTemplateKind.NonRenewal, "Notice of Non-Renewal"),
        (NoticeTemplateKind.RentIncrease, "Rent Increase Notification"),
        (NoticeTemplateKind.LeaseViolation, "Lease Violation Notice"),
        (NoticeTemplateKind.EntryNotice, "24-Hour Entry Notice"),
        (NoticeTemplateKind.LateRentReminder, "Late Rent Reminder"),
    };

    public static NoticeTemplate Build(
        NoticeTemplateKind kind,
        Lease lease,
        decimal outstandingBalance)
    {
        var property = lease.Property!;
        var address = $"{property.AddressLine1}, {property.City}, {property.State} {property.PostalCode}";
        var tenantNames = lease.TenantNames;
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("MMMM d, yyyy");

        return kind switch
        {
            NoticeTemplateKind.PayOrQuit => new(
                kind, "Pay or Quit",
                "NOTICE TO PAY RENT OR QUIT",
                $@"TO: {tenantNames}
RE: {address}

You are hereby notified that rent in the amount of {outstandingBalance:C} is now due and unpaid for the premises you occupy at the address above.

You must pay the full amount owed WITHIN THREE (3) DAYS from receipt of this notice or vacate and surrender possession of the premises. Failure to comply will result in the initiation of unlawful detainer proceedings.

If you have already paid, please disregard this notice.

Dated: {today}"),

            NoticeTemplateKind.NonRenewal => new(
                kind, "Notice of Non-Renewal",
                "NOTICE OF NON-RENEWAL OF LEASE",
                $@"TO: {tenantNames}
RE: {address}

Please be advised that your lease at the above premises will not be renewed and will terminate on {lease.EndDate:MMMM d, yyyy}.

You are required to vacate and return possession of the premises on or before that date. Please contact us to schedule a move-out inspection at your earliest convenience.

Dated: {today}"),

            NoticeTemplateKind.RentIncrease => new(
                kind, "Rent Increase",
                "NOTICE OF RENT INCREASE",
                $@"TO: {tenantNames}
RE: {address}

Please be advised that effective on the first rent due date at least 30 days from receipt of this notice, the monthly rent for the above premises will be adjusted.

Current monthly rent: {lease.MonthlyRent:C}
New monthly rent: [ENTER NEW AMOUNT]
Effective date: [ENTER EFFECTIVE DATE]

All other terms of your lease remain unchanged.

Dated: {today}"),

            NoticeTemplateKind.LeaseViolation => new(
                kind, "Lease Violation",
                "NOTICE OF LEASE VIOLATION",
                $@"TO: {tenantNames}
RE: {address}

This is to notify you of the following violation of your lease agreement:

[DESCRIBE VIOLATION]

You are required to cure the violation within [NUMBER] days of receipt of this notice. Failure to do so may result in termination of your tenancy.

Dated: {today}"),

            NoticeTemplateKind.EntryNotice => new(
                kind, "24-Hour Entry Notice",
                "24-HOUR NOTICE OF INTENT TO ENTER",
                $@"TO: {tenantNames}
RE: {address}

Pursuant to applicable law, this is written notice that the landlord or their agent intends to enter the premises for the following purpose:

Date of entry: [ENTER DATE]
Approximate time: [ENTER TIME WINDOW]
Reason: [e.g. routine inspection, repairs, showing to prospective tenant]

If the above date/time is inconvenient, please contact us to reschedule.

Dated: {today}"),

            NoticeTemplateKind.LateRentReminder => new(
                kind, "Late Rent Reminder",
                "REMINDER: RENT PAST DUE",
                $@"TO: {tenantNames}
RE: {address}

Our records show an outstanding balance of {outstandingBalance:C} on your account. Rent is now past due.

Please remit payment as soon as possible to avoid additional late fees. If you've already paid, thank you — please disregard this reminder.

Dated: {today}"),

            _ => new(
                NoticeTemplateKind.Custom, "Custom notice",
                "NOTICE",
                $@"TO: {tenantNames}
RE: {address}

[Enter your notice text here.]

Dated: {today}"),
        };
    }
}
