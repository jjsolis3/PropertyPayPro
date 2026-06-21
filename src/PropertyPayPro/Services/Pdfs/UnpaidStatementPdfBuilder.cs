using PropertyPayPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services.Pdfs;

public static class UnpaidStatementPdfBuilder
{
    public static byte[] Build(Lease lease, IReadOnlyList<RentalCharge> unpaid, DateOnly reportDate, byte[]? logo)
    {
        var property = lease.Property!;
        var reference = $"Statement for {reportDate:MMMM d, yyyy}";

        return BrandedDocumentLayout.BuildLetterDocument("Rental Account Statement", reference, logo, content =>
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

                col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Unpaid Invoices"));

                if (!unpaid.Any())
                {
                    col.Item().Background(PdfBrand.SuccessBg).Padding(12).Text("No unpaid invoices. Account is current.")
                        .FontColor(PdfBrand.SuccessText).Bold();
                }
                else
                {
                    col.Item().Element(c => RenderUnpaidTable(c, unpaid));

                    var totalDue = unpaid.Sum(c => c.AmountDue);
                    var totalPaid = unpaid.Sum(c => c.AmountPaid);
                    var totalBal = unpaid.Sum(c => c.Balance);

                    col.Item().PaddingTop(12).Background(PdfBrand.HeaderBg).Padding(14).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("TOTAL OUTSTANDING").Bold().FontColor(PdfBrand.TextMuted);
                            c.Item().Text($"Billed {totalDue:C} • Paid {totalPaid:C}")
                                .FontSize(9).FontColor(PdfBrand.TextMuted);
                        });
                        row.ConstantItem(160).AlignRight().AlignMiddle().Text(totalBal.ToString("C"))
                            .FontSize(20).Bold().FontColor(PdfBrand.DangerText);
                    });

                    col.Item().PaddingTop(16).Text(
                        "Please remit payment for any outstanding balance at your earliest convenience. " +
                        "If you've already paid, please disregard this statement.")
                        .FontColor(PdfBrand.TextMuted).Italic();
                }
            });
        });
    }

    private static void RenderUnpaidTable(IContainer container, IReadOnlyList<RentalCharge> unpaid)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
            });

            t.Header(h =>
            {
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Invoice #").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Period").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Due date").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).AlignRight().Text("Amount due").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).AlignRight().Text("Paid").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).AlignRight().Text("Balance").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Status").Bold();
            });

            var rowIdx = 0;
            foreach (var c in unpaid)
            {
                var bg = (rowIdx++ % 2 == 0) ? "#ffffff" : PdfBrand.RowAlt;
                var statusText = c.Status switch
                {
                    ChargeStatus.Overdue => "Overdue",
                    ChargeStatus.PartiallyPaid => "Partial",
                    _ => "Unpaid"
                };
                var statusColor = c.Status == ChargeStatus.Overdue ? PdfBrand.DangerText : PdfBrand.TextDark;

                t.Cell().Background(bg).Padding(6).Text(c.Id.ToString("D8"));
                t.Cell().Background(bg).Padding(6).Text(c.BillingPeriodStart.ToString("MMM yyyy"));
                t.Cell().Background(bg).Padding(6).Text(c.DueDate.ToString("yyyy-MM-dd"));
                t.Cell().Background(bg).Padding(6).AlignRight().Text(c.AmountDue.ToString("C"));
                t.Cell().Background(bg).Padding(6).AlignRight().Text(c.AmountPaid.ToString("C"));
                t.Cell().Background(bg).Padding(6).AlignRight().Text(c.Balance.ToString("C")).Bold();
                t.Cell().Background(bg).Padding(6).Text(statusText).FontColor(statusColor).Bold();
            }
        });
    }
}
