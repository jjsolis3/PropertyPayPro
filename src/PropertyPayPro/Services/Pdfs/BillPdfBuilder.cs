using PropertyPayPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services.Pdfs;

public static class BillPdfBuilder
{
    public static byte[] Build(RentalCharge charge, byte[]? logo)
    {
        var lease = charge.Lease!;
        var property = lease.Property!;
        var title = charge.Kind == ChargeKind.LateFee ? "Late Fee Invoice" : "Rent Invoice";
        var reference = $"Invoice #{charge.Id:D8}  •  {charge.BillingPeriodStart:MMMM yyyy}";

        return BrandedDocumentLayout.BuildLetterDocument(title, reference, logo, content =>
        {
            content.Column(col =>
            {
                col.Spacing(8);

                col.Item().Element(c => BrandedDocumentLayout.TwoColumnAddress(c,
                    "Property",
                    new[]
                    {
                        property.Name,
                        property.AddressLine1,
                        $"{property.City}, {property.State} {property.PostalCode}"
                    },
                    lease.Tenants.Count > 1 ? "Tenants" : "Tenant",
                    lease.Tenants.Select(t => string.IsNullOrWhiteSpace(t.Email)
                        ? t.DisplayName
                        : $"{t.DisplayName} — {t.Email}")));

                col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Charge Details"));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Billing period", charge.BillingPeriodStart.ToString("MMMM yyyy")));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Issued", DateTime.UtcNow.ToString("MMMM d, yyyy")));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Due date", charge.DueDate.ToString("MMMM d, yyyy")));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Type", charge.Kind == ChargeKind.LateFee ? "Late Fee" : "Monthly Rent"));

                col.Item().PaddingTop(16).Border(1).BorderColor(PdfBrand.Primary).Padding(16).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text("Amount Due")
                        .FontSize(14).Bold().FontColor(PdfBrand.TextMuted);
                    row.ConstantItem(180).AlignRight().AlignMiddle().Text(charge.AmountDue.ToString("C"))
                        .FontSize(22).Bold().FontColor(PdfBrand.Primary);
                });

                if (!string.IsNullOrWhiteSpace(charge.Notes))
                {
                    col.Item().PaddingTop(8).Text(t =>
                    {
                        t.Span("Notes: ").Bold();
                        t.Span(charge.Notes);
                    });
                }

                col.Item().PaddingTop(20).Text(
                    "Please remit payment by the due date. If you've already paid, please disregard this invoice.")
                    .FontColor(PdfBrand.TextMuted).Italic();
            });
        });
    }
}
