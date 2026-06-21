using PropertyPayPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services.Pdfs;

public static class BillPaidConfirmationPdfBuilder
{
    public static byte[] Build(RentalCharge charge, IReadOnlyList<PaymentAllocation> allocations, byte[]? logo)
    {
        var lease = charge.Lease!;
        var property = lease.Property!;
        var reference = $"Invoice #{charge.Id:D8}  •  {charge.BillingPeriodStart:MMMM yyyy}";
        var lastPaidOn = allocations.Max(a => a.Payment?.PaidOn) ?? DateOnly.FromDateTime(DateTime.UtcNow);

        return BrandedDocumentLayout.BuildLetterDocument("Bill Paid in Full", reference, logo, content =>
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

                col.Item().PaddingTop(12).Background(PdfBrand.SuccessBg).Padding(16).Column(c =>
                {
                    c.Item().Text("PAID IN FULL").FontSize(18).Bold().FontColor(PdfBrand.SuccessText);
                    c.Item().Text($"{charge.BillingPeriodStart:MMMM yyyy} rent has been fully paid as of {lastPaidOn:MMMM d, yyyy}.")
                        .FontColor(PdfBrand.SuccessText);
                });

                col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Bill Summary"));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Billing period", charge.BillingPeriodStart.ToString("MMMM yyyy")));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Due date", charge.DueDate.ToString("MMMM d, yyyy")));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Total billed", charge.AmountDue.ToString("C")));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Total paid", charge.AmountPaid.ToString("C")));

                col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Payments applied"));
                col.Item().Element(c => RenderAllocations(c, allocations));

                col.Item().PaddingTop(16).Text(
                    "This document confirms the above bill has been settled in full. " +
                    "Please retain it for your records.")
                    .FontColor(PdfBrand.TextMuted).Italic();
            });
        });
    }

    private static void RenderAllocations(IContainer container, IReadOnlyList<PaymentAllocation> allocations)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1);
                c.RelativeColumn(2);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
            });

            t.Header(h =>
            {
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Date").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Paid by").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Method").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Reference").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).AlignRight().Text("Applied").Bold();
            });

            var rowIdx = 0;
            foreach (var a in allocations.OrderBy(x => x.Payment?.PaidOn))
            {
                var p = a.Payment!;
                var bg = (rowIdx++ % 2 == 0) ? "#ffffff" : PdfBrand.RowAlt;
                var leaseTenants = p.Lease?.TenantNames ?? "—";

                t.Cell().Background(bg).Padding(6).Text(p.PaidOn.ToString("yyyy-MM-dd"));
                t.Cell().Background(bg).Padding(6).Text(leaseTenants);
                t.Cell().Background(bg).Padding(6).Text(p.Method.ToString());
                t.Cell().Background(bg).Padding(6).Text(p.Reference ?? "—");
                t.Cell().Background(bg).Padding(6).AlignRight().Text(a.Amount.ToString("C")).Bold();
            }

            t.Cell().ColumnSpan(4).Padding(6).AlignRight().Text("Total applied").Bold();
            t.Cell().Padding(6).AlignRight().Text(allocations.Sum(a => a.Amount).ToString("C"))
                .Bold().FontColor(PdfBrand.SuccessText);
        });
    }
}
