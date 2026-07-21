using PropertyPayPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services.Pdfs;

public static class InspectionPdfBuilder
{
    public static byte[] Build(
        Inspection inspection,
        byte[]? logo)
    {
        var lease = inspection.Lease!;
        var property = lease.Property!;
        var title = inspection.Kind == InspectionKind.MoveIn
            ? "Move-in Inspection Report"
            : "Move-out Inspection Report";
        var reference = $"{property.Name}  •  {inspection.ScheduledFor:MMMM d, yyyy}"
            + (inspection.CompletedOn.HasValue ? $"  •  Completed {inspection.CompletedOn:MMM d, yyyy}" : "");

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

                col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Inspection Details"));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Type",
                    inspection.Kind == InspectionKind.MoveIn ? "Move-in" : "Move-out"));
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Scheduled",
                    inspection.ScheduledFor.ToString("MMMM d, yyyy")));
                if (inspection.CompletedOn.HasValue)
                {
                    col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Completed",
                        inspection.CompletedOn.Value.ToString("MMMM d, yyyy")));
                }
                if (!string.IsNullOrWhiteSpace(inspection.ConductedBy))
                {
                    col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Conducted by", inspection.ConductedBy));
                }
                col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Tenant present",
                    inspection.TenantPresent ? "Yes" : "No"));

                if (!string.IsNullOrWhiteSpace(inspection.OverallNotes))
                {
                    col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, "Overall Notes"));
                    col.Item().Text(inspection.OverallNotes);
                }

                // Group items by Room, ordered by Order within each room.
                var roomGroups = inspection.Items
                    .OrderBy(i => i.Order)
                    .GroupBy(i => i.Room)
                    .ToList();
                foreach (var group in roomGroups)
                {
                    col.Item().Element(c => BrandedDocumentLayout.SectionHeading(c, group.Key));
                    col.Item().Element(c => RenderRoomTable(c, group.ToList(), inspection.Kind == InspectionKind.MoveOut));
                }

                // Move-out summary — deposit deduction totals.
                if (inspection.Kind == InspectionKind.MoveOut)
                {
                    var totalDeductions = inspection.Items.Sum(i => i.DeductionAmount ?? 0m);
                    var deposit = lease.SecurityDeposit;
                    var refund = Math.Max(0m, deposit - totalDeductions);

                    col.Item().PaddingTop(16).Element(c => BrandedDocumentLayout.SectionHeading(c, "Security Deposit Summary"));
                    col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Deposit on file", deposit.ToString("C")));
                    col.Item().Element(c => BrandedDocumentLayout.LabelValueRow(c, "Total deductions", totalDeductions.ToString("C")));
                    col.Item().Background(PdfBrand.SuccessBg).Padding(12).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text("Refund to tenant")
                            .FontSize(14).Bold().FontColor(PdfBrand.SuccessText);
                        row.ConstantItem(160).AlignRight().AlignMiddle().Text(refund.ToString("C"))
                            .FontSize(18).Bold().FontColor(PdfBrand.SuccessText);
                    });
                }

                // Signature block.
                col.Item().PaddingTop(24).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingTop(20).LineHorizontal(1).LineColor(PdfBrand.TextDark);
                        c.Item().PaddingTop(2).Text("Landlord / Agent").FontSize(9).FontColor(PdfBrand.TextMuted);
                    });
                    row.ConstantItem(30);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingTop(20).LineHorizontal(1).LineColor(PdfBrand.TextDark);
                        c.Item().PaddingTop(2).Text("Tenant").FontSize(9).FontColor(PdfBrand.TextMuted);
                    });
                });
            });
        });
    }

    private static void RenderRoomTable(IContainer container, IList<InspectionItem> items, bool includeDeductions)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(3);
                if (includeDeductions) c.RelativeColumn(1.2f);
            });

            t.Header(h =>
            {
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Item").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Condition").Bold();
                h.Cell().Background(PdfBrand.HeaderBg).Padding(6).Text("Notes").Bold();
                if (includeDeductions)
                    h.Cell().Background(PdfBrand.HeaderBg).Padding(6).AlignRight().Text("Deduction").Bold();
            });

            foreach (var item in items)
            {
                t.Cell().BorderBottom(0.5f).BorderColor(PdfBrand.BorderLight).Padding(6).Text(item.Item);
                t.Cell().BorderBottom(0.5f).BorderColor(PdfBrand.BorderLight).Padding(6).Text(item.Condition.ToString());

                var noteText = item.Notes ?? "";
                if (item.Photos.Any())
                {
                    var photosLine = $" [{item.Photos.Count} photo(s) on file]";
                    noteText = string.IsNullOrWhiteSpace(noteText) ? photosLine.TrimStart() : noteText + photosLine;
                }
                t.Cell().BorderBottom(0.5f).BorderColor(PdfBrand.BorderLight).Padding(6).Text(noteText);

                if (includeDeductions)
                {
                    var cell = t.Cell().BorderBottom(0.5f).BorderColor(PdfBrand.BorderLight).Padding(6).AlignRight();
                    if (item.DeductionAmount.HasValue && item.DeductionAmount.Value > 0)
                    {
                        cell.Column(c =>
                        {
                            c.Item().Text(item.DeductionAmount.Value.ToString("C")).Bold();
                            if (!string.IsNullOrWhiteSpace(item.DeductionReason))
                            {
                                c.Item().Text(item.DeductionReason).FontSize(8).FontColor(PdfBrand.TextMuted);
                            }
                        });
                    }
                    else
                    {
                        cell.Text("—").FontColor(PdfBrand.TextMuted);
                    }
                }
            }
        });
    }
}
