using PropertyPayPro.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services.Pdfs;

public static class NoticePdfBuilder
{
    public static byte[] Build(
        string title,
        string body,
        Lease lease,
        byte[]? logo)
    {
        var property = lease.Property!;
        var reference = $"{property.Name}  •  {DateOnly.FromDateTime(DateTime.UtcNow):MMMM d, yyyy}";

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

                col.Item().PaddingTop(12).Element(c => BrandedDocumentLayout.SectionHeading(c, title));

                col.Item().PaddingTop(4).Text(body).LineHeight(1.4f);

                col.Item().PaddingTop(24).LineHorizontal(0.5f).LineColor(PdfBrand.BorderLight);

                col.Item().PaddingTop(24).Text("Landlord / Authorized Agent").Bold();
                col.Item().PaddingTop(20).LineHorizontal(1).LineColor(PdfBrand.TextDark);
                col.Item().PaddingTop(2).Text("Signature").FontSize(9).FontColor(PdfBrand.TextMuted);
            });
        });
    }
}
