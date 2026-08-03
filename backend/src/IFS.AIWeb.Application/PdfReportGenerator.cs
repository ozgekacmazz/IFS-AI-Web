using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace IFS.AIWeb.Application;

public interface IPdfReportGenerator
{
    byte[] Generate(SummaryDetailResponse detail);
}

public sealed class PdfReportGenerator : IPdfReportGenerator
{
    static PdfReportGenerator()
    {
        if (GlobalFontSettings.FontResolver is not EmbeddedFontResolver)
        {
            GlobalFontSettings.FontResolver = EmbeddedFontResolver.Instance;
        }
    }

    public byte[] Generate(SummaryDetailResponse detail)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"IFS AI-Web Summary Report - {detail.Id}";
        document.Info.Author = "IFS AI-Web";
        document.Info.Subject = "AI Summary & Source Text Report";

        const double marginLeft = 40;
        const double marginTop = 40;
        const double marginRight = 40;
        const double marginBottom = 50;

        var fontTitle = new XFont("NotoSans", 16, XFontStyleEx.Bold);
        var fontSubTitle = new XFont("NotoSans", 10, XFontStyleEx.Bold);
        var fontMeta = new XFont("NotoSans", 9, XFontStyleEx.Regular);
        var fontSectionTitle = new XFont("NotoSans", 11, XFontStyleEx.Bold);
        var fontBody = new XFont("NotoSans", 9.5, XFontStyleEx.Regular);
        var fontFooter = new XFont("NotoSans", 8, XFontStyleEx.Regular);

        var primaryColor = XColor.FromArgb(30, 41, 59); // Dark slate (#1e293b)
        var secondaryColor = XColor.FromArgb(71, 85, 105); // Slate (#475569)
        var summaryBgColor = XColor.FromArgb(241, 245, 249); // Slate-100 (#f1f5f9)
        var summaryBorderColor = XColor.FromArgb(148, 163, 184); // Slate-400 (#94a3b8)
        var accentColor = XColor.FromArgb(37, 99, 235); // Blue-600 (#2563eb)

        var currentPdfPage = document.AddPage();
        currentPdfPage.Size = PageSize.A4;
        var gfx = XGraphics.FromPdfPage(currentPdfPage);

        var pageWidth = currentPdfPage.Width.Point;
        var pageHeight = currentPdfPage.Height.Point;
        var contentWidth = pageWidth - marginLeft - marginRight;
        var maxY = pageHeight - marginBottom;

        double currentY = marginTop;

        // --- HEADER ---
        gfx.DrawString("IFS AI-Web", fontSubTitle, new XSolidBrush(accentColor), marginLeft, currentY);
        currentY += 15;
        gfx.DrawString("ÖZET VE KAYNAK METİN RAPORU", fontTitle, new XSolidBrush(primaryColor), marginLeft, currentY);
        currentY += 25;

        // Separator line
        gfx.DrawLine(new XPen(summaryBorderColor, 1), marginLeft, currentY, pageWidth - marginRight, currentY);
        currentY += 15;

        // --- METADATA STRIP ---
        var dateFormatted = detail.CreatedAtUtc.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var langFormatted = detail.Language.Equals("Turkish", StringComparison.OrdinalIgnoreCase) ? "Türkçe" : "English";

        var metaText1 = $"Tarih: {dateFormatted}   |   Dil: {langFormatted}";
        var metaText2 = $"Özet ID: {detail.Id}";
        gfx.DrawString(metaText1, fontMeta, new XSolidBrush(secondaryColor), marginLeft, currentY);
        gfx.DrawString(metaText2, fontMeta, new XSolidBrush(secondaryColor), pageWidth - marginRight - gfx.MeasureString(metaText2, fontMeta).Width, currentY);
        currentY += 20;

        void EnsureSpace(double requiredHeight)
        {
            if (currentY + requiredHeight > maxY)
            {
                gfx.Dispose();
                currentPdfPage = document.AddPage();
                currentPdfPage.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(currentPdfPage);
                currentY = marginTop;
            }
        }

        // --- SUMMARY SECTION ---
        EnsureSpace(40);
        gfx.DrawString("ÖZET", fontSectionTitle, new XSolidBrush(primaryColor), marginLeft, currentY);
        currentY += 16;

        var summaryLines = WrapText(gfx, detail.Summary, fontBody, contentWidth - 20);
        double summaryBoxPadding = 10;
        double summaryLineHeight = fontBody.GetHeight() + 3;
        double summaryContentHeight = Math.Max(1, summaryLines.Count) * summaryLineHeight;
        double summaryBoxHeight = summaryContentHeight + (summaryBoxPadding * 2);

        EnsureSpace(summaryBoxHeight + 10);

        // Draw summary background box & border
        var boxRect = new XRect(marginLeft, currentY, contentWidth, summaryBoxHeight);
        gfx.DrawRectangle(new XSolidBrush(summaryBgColor), boxRect);
        gfx.DrawRectangle(new XPen(accentColor, 1.5), boxRect);

        double summaryTextY = currentY + summaryBoxPadding + fontBody.GetHeight();
        foreach (var line in summaryLines)
        {
            gfx.DrawString(line, fontBody, new XSolidBrush(primaryColor), marginLeft + summaryBoxPadding, summaryTextY);
            summaryTextY += summaryLineHeight;
        }
        currentY += summaryBoxHeight + 25;

        // --- SOURCE TEXT SECTION ---
        EnsureSpace(40);
        gfx.DrawString("KAYNAK METİN", fontSectionTitle, new XSolidBrush(primaryColor), marginLeft, currentY);
        currentY += 16;

        var sourceLines = WrapText(gfx, detail.InputText, fontBody, contentWidth);
        double sourceLineHeight = fontBody.GetHeight() + 3;

        foreach (var line in sourceLines)
        {
            EnsureSpace(sourceLineHeight);
            if (string.IsNullOrEmpty(line))
            {
                currentY += sourceLineHeight / 2;
                continue;
            }
            gfx.DrawString(line, fontBody, new XSolidBrush(secondaryColor), marginLeft, currentY + fontBody.GetHeight());
            currentY += sourceLineHeight;
        }

        gfx.Dispose();

        // --- FOOTERS (Draw page numbers on all pages) ---
        int totalPages = document.PageCount;
        for (int i = 0; i < totalPages; i++)
        {
            var page = document.Pages[i];
            using var footerGfx = XGraphics.FromPdfPage(page);
            var footerY = pageHeight - 25;
            footerGfx.DrawLine(new XPen(summaryBorderColor, 0.5), marginLeft, footerY - 10, pageWidth - marginRight, footerY - 10);

            var leftFooter = "IFS AI-Web Otomatik Raporlama";
            var rightFooter = $"Sayfa {i + 1} / {totalPages}";
            footerGfx.DrawString(leftFooter, fontFooter, new XSolidBrush(secondaryColor), marginLeft, footerY);
            footerGfx.DrawString(rightFooter, fontFooter, new XSolidBrush(secondaryColor), pageWidth - marginRight - footerGfx.MeasureString(rightFooter, fontFooter).Width, footerY);
        }

        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }

    private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return lines;

        var paragraphs = text.Replace("\r\n", "\n").Split('\n');
        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                lines.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ');
            var currentLine = string.Empty;
            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                var size = gfx.MeasureString(testLine, font);
                if (size.Width > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }
        }
        return lines;
    }
}
