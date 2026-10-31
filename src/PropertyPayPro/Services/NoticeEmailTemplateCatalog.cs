using PropertyPayPro.Models;

namespace PropertyPayPro.Services;

public enum NoticeEmailTemplateKind
{
    Custom,
    FormalNoticeDelivery,
    RentIncreaseDelivery,
    LeaseRenewalDelivery,
    PayOrQuitDelivery,
    GeneralCorrespondence
}

public record NoticeEmailTemplate(
    NoticeEmailTemplateKind Kind,
    string Label,
    string Subject,
    string Body);

/// <summary>
/// Cover-email templates for sending generated notices (and any
/// additional attachments) to tenants. Separate concern from the
/// NOTICE body templates — this one wraps the delivery email, that
/// one is the content of the PDF itself.
///
/// Each template returns Subject + plain-text Body prefilled from
/// lease context; the admin edits both in the Send composer. Body is
/// plain text — newlines become <br /> in the composed email via
/// EmailComposer.ComposeNoticeDelivery.
/// </summary>
public static class NoticeEmailTemplateCatalog
{
    public static IReadOnlyList<(NoticeEmailTemplateKind Kind, string Label)> Kinds { get; } = new[]
    {
        (NoticeEmailTemplateKind.Custom, "Custom cover email"),
        (NoticeEmailTemplateKind.FormalNoticeDelivery, "Formal notice delivery"),
        (NoticeEmailTemplateKind.RentIncreaseDelivery, "Rent increase delivery"),
        (NoticeEmailTemplateKind.LeaseRenewalDelivery, "Lease renewal package"),
        (NoticeEmailTemplateKind.PayOrQuitDelivery, "Pay-or-quit delivery"),
        (NoticeEmailTemplateKind.GeneralCorrespondence, "General correspondence"),
    };

    public static NoticeEmailTemplate Build(
        NoticeEmailTemplateKind kind,
        Lease lease,
        string noticeTitle)
    {
        var property = lease.Property!;
        var address = $"{property.AddressLine1}, {property.City}, {property.State} {property.PostalCode}";
        var tenantNames = lease.TenantNames;

        return kind switch
        {
            NoticeEmailTemplateKind.FormalNoticeDelivery => new(
                kind, "Formal notice delivery",
                $"Formal notice regarding {property.Name}",
                $@"Dear {tenantNames},

Please find attached a formal notice regarding the premises at {address}.

Review the attached document carefully. If you have questions or
believe you have received it in error, contact us as soon as possible.

Thank you,
Property Management"),

            NoticeEmailTemplateKind.RentIncreaseDelivery => new(
                kind, "Rent increase delivery",
                $"Notice of rent increase — {property.Name}",
                $@"Dear {tenantNames},

Please find attached formal notice of a rent adjustment on your lease
at {address}, along with any supporting documents.

The attachment(s) describe the new rent amount and effective date.
If you have questions, please reach out before the effective date.

Thank you,
Property Management"),

            NoticeEmailTemplateKind.LeaseRenewalDelivery => new(
                kind, "Lease renewal package",
                $"Lease renewal — {property.Name}",
                $@"Dear {tenantNames},

We're pleased to offer a renewal of your lease at {address}. Please
find the renewal documents attached for your review.

Review the attachments carefully, let us know if you have any
questions, and return the signed documents by the date noted on the
renewal.

Thank you,
Property Management"),

            NoticeEmailTemplateKind.PayOrQuitDelivery => new(
                kind, "Pay-or-quit delivery",
                $"IMPORTANT: Notice to pay rent or quit — {property.Name}",
                $@"Dear {tenantNames},

A formal Notice to Pay Rent or Quit for the premises at {address} is
attached to this email.

The notice specifies the amount owed and the time period in which to
cure. Failure to comply may result in the initiation of unlawful
detainer proceedings. If payment has already been remitted, please
disregard.

Property Management"),

            NoticeEmailTemplateKind.GeneralCorrespondence => new(
                kind, "General correspondence",
                noticeTitle,
                $@"Dear {tenantNames},

Please see the attached regarding your lease at {address}.

Thank you,
Property Management"),

            _ => new(
                NoticeEmailTemplateKind.Custom, "Custom cover email",
                noticeTitle,
                $@"Dear {tenantNames},

[Enter your cover message here.]

Thank you,
Property Management"),
        };
    }
}
