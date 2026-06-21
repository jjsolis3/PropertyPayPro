using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PropertyPayPro.Services.Pdfs;

public static class BrandedDocumentLayout
{
    public static byte[] BuildLetterDocument(
        string documentTitle,
        string? referenceLabel,
        byte[]? logoBytes,
        Action<IContainer> content)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0.6f, Unit.Inch);
                page.DefaultTextStyle(t => t.FontFamily(PdfBrand.FontFamily).FontSize(10).FontColor(PdfBrand.TextDark));

                page.Header().Element(c => Header(c, documentTitle, referenceLabel, logoBytes));
                page.Content().PaddingVertical(12).Element(content);
                page.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, string title, string? reference, byte[]? logo)
    {
        container.PaddingBottom(8).BorderBottom(1).BorderColor(PdfBrand.BorderLight).Row(row =>
        {
            row.ConstantItem(120).AlignMiddle().Element(c =>
            {
                if (logo is { Length: > 0 })
                {
                    c.Height(40).Image(logo).FitArea();
                }
                else
                {
                    c.Text(PdfBrand.AppName).FontSize(14).Bold().FontColor(PdfBrand.Primary);
                }
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text(title).FontSize(16).Bold().FontColor(PdfBrand.Primary);
                if (!string.IsNullOrWhiteSpace(reference))
                {
                    col.Item().Text(reference).FontSize(9).FontColor(PdfBrand.TextMuted);
                }
                col.Item().Text($"Generated {DateTime.UtcNow.ToLocalTime():MMM d, yyyy h:mm tt}")
                    .FontSize(8).FontColor(PdfBrand.TextMuted);
            });
        });
    }

    private static void Footer(IContainer container)
    {
        container.PaddingTop(8).BorderTop(1).BorderColor(PdfBrand.BorderLight).Row(row =>
        {
            row.RelativeItem().Text(PdfBrand.AppName)
                .FontSize(8).FontColor(PdfBrand.TextMuted);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(PdfBrand.TextMuted));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }

    // ---- shared bits used by all templates ----

    public static void SectionHeading(IContainer container, string text) =>
        container.PaddingTop(8).PaddingBottom(4).Text(text)
            .FontSize(12).Bold().FontColor(PdfBrand.Primary);

    public static void LabelValueRow(IContainer container, string label, string value)
    {
        container.PaddingVertical(2).Row(r =>
        {
            r.ConstantItem(120).Text(label).Bold().FontColor(PdfBrand.TextMuted);
            r.RelativeItem().Text(value);
        });
    }

    public static void TwoColumnAddress(
        IContainer container,
        string leftHeader, IEnumerable<string> leftLines,
        string rightHeader, IEnumerable<string> rightLines)
    {
        container.Row(row =>
        {
            row.RelativeItem().PaddingRight(12).Column(col =>
            {
                col.Item().Text(leftHeader).Bold().FontColor(PdfBrand.TextMuted);
                foreach (var line in leftLines.Where(s => !string.IsNullOrWhiteSpace(s)))
                    col.Item().Text(line);
            });
            row.RelativeItem().BorderLeft(1).BorderColor(PdfBrand.BorderLight).PaddingLeft(12).Column(col =>
            {
                col.Item().Text(rightHeader).Bold().FontColor(PdfBrand.TextMuted);
                foreach (var line in rightLines.Where(s => !string.IsNullOrWhiteSpace(s)))
                    col.Item().Text(line);
            });
        });
    }
}
