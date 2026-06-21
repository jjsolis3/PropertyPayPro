using PropertyPayPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services.Pdfs;

public static class ReceiptPdfBuilder
{
    public static byte[] Build(RentPayment payment, byte[]? logo)
    {
        var lease = payment.Lease!;
        var property = lease.Property!;
        var reference = $"Receipt #{payment.Id:D8}  •  {payment.PaidOn:MMMM d, yyyy}";

        return BrandedDocumentLayout.BuildLetterDocument("Payment Receipt", reference, logo, content =>
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

                col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Payment Details"));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Date received", payment.PaidOn.ToString("MMMM d, yyyy")));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Method", payment.Method.ToString()));
                if (!string.IsNullOrWhiteSpace(payment.Reference))
                    col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Reference", payment.Reference));

                col.Item().PaddingTop(12).Background(PdfBrand.SuccessBg).Padding(14).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text("Amount Received")
                        .FontSize(14).Bold().FontColor(PdfBrand.SuccessText);
                    row.ConstantItem(180).AlignRight().AlignMiddle().Text(payment.Amount.ToString("C"))
                        .FontSize(22).Bold().FontColor(PdfBrand.SuccessText);
                });

                col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Applied to"));
                if (payment.Allocations.Any())
                {
                    col.Item().Element(c => RenderAllocations(c, payment));
                }
                else
                {
                    col.Item().Background(PdfBrand.HeaderBg).Padding(12).Text(
                        "No allocations recorded — the full amount is held as a credit on this lease.")
                        .FontColor(PdfBrand.TextMuted).Italic();
                }

                if (!string.IsNullOrWhiteSpace(payment.Notes))
                {
                    col.Item().PaddingTop(8).Text(t =>
                    {
                        t.Span("Notes: ").Bold();
                        t.Span(payment.Notes);
                    });
                }

                col.Item().PaddingTop(16).Text("Thank you for your payment.")
                    .FontColor(PdfBrand.TextMuted).Italic();
            });
        });
    }

    private static void RenderAllocations(IContainer container, RentPayment payment)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(1.5f);
                c.RelativeColumn(1);
            });

            t.Header(h =>
            {
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Billing period").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Due date").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).AlignRight().Text("Applied").Bold();
            });

            foreach (var a in payment.Allocations.OrderBy(x => x.RentalCharge?.BillingPeriodStart))
            {
                t.Cell().BorderBottom(0.5f).BorderColor(PdfBrand.BorderLight).Padding(6).Text(a.RentalCharge!.PeriodLabel);
                t.Cell().BorderBottom(0.5f).BorderColor(PdfBrand.BorderLight).Padding(6).Text(a.RentalCharge.DueDate.ToString("yyyy-MM-dd"));
                t.Cell().BorderBottom(0.5f).BorderColor(PdfBrand.BorderLight).Padding(6).AlignRight().Text(a.Amount.ToString("C"));
            }

            t.Cell().ColumnSpan(2).Padding(6).AlignRight().Text("Total applied").Bold();
            t.Cell().Padding(6).AlignRight().Text(payment.AllocatedAmount.ToString("C")).Bold();

            if (payment.UnallocatedAmount > 0)
            {
                t.Cell().ColumnSpan(2).Padding(6).AlignRight().Text("Unallocated (credit)").FontColor(PdfBrand.Accent);
                t.Cell().Padding(6).AlignRight().Text(payment.UnallocatedAmount.ToString("C")).FontColor(PdfBrand.Accent);
            }
        });
    }
}
