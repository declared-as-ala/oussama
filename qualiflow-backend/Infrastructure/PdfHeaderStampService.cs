using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocApi.Infrastructure
{
    public sealed class PdfHeaderStampService : IPdfHeaderStampService
    {
        private const string HeaderStampKeyword = "QualiFlowHeaderStampedV4";
        private readonly ILogger<PdfHeaderStampService> _logger;
        private readonly bool _enabled;
        private readonly string? _organizationLogosPath;
        private readonly string? _defaultLogoPath;

        public PdfHeaderStampService(IConfiguration configuration, ILogger<PdfHeaderStampService> logger)
        {
            _logger = logger;
            _enabled = configuration.GetValue("Storage:PdfHeaderEnabled", true);
            _organizationLogosPath = ResolveOptionalPath(configuration["Storage:OrganizationLogosPath"]);
            _defaultLogoPath = ResolveOptionalPath(configuration["Storage:DefaultLogoPath"]);
        }

        public async Task<Stream> AddHeaderAsync(Stream sourcePdfStream, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default)
        {
            var sourceCopy = await CopyToMemoryAsync(sourcePdfStream, cancellationToken);

            if (!_enabled)
            {
                _logger.LogInformation("PDF header stamping is disabled (PdfHeaderEnabled=false). Returning original stream.");
                sourceCopy.Position = 0;
                return sourceCopy;
            }

            if (!LooksLikePdf(sourceCopy))
            {
                _logger.LogWarning("Source stream does not appear to be a valid PDF (missing %PDF header). Returning original stream.");
                sourceCopy.Position = 0;
                return sourceCopy;
            }

            _logger.LogInformation("Starting PDF header stamping. Document: {Code}, Version: {Version}, Size: {Size} bytes.",
                metadata.DocumentCode, metadata.VersionNumber, sourceCopy.Length);

            try
            {
                // Always re-stamp: documents are stored pristine. Extract original keywords to append to.
                string? sourceKeywords = null;
                try
                {
                    sourceCopy.Position = 0;
                    using var tempCopy = new MemoryStream(sourceCopy.ToArray());
                    using var pdfDocument = PdfReader.Open(tempCopy, PdfDocumentOpenMode.InformationOnly);
                    sourceKeywords = pdfDocument.Info.Keywords;
                }
                catch (Exception kwEx)
                {
                    _logger.LogWarning(kwEx, "Could not read existing PDF keywords — stamping will proceed with null keywords.");
                }

                sourceCopy.Position = 0;
                var stampedStream = RebuildPdfWithHeader(sourceCopy, metadata, sourceKeywords, cancellationToken);
                sourceCopy.Dispose();
                _logger.LogInformation("PDF header stamping succeeded. Output size: {Size} bytes.", stampedStream.Length);
                return stampedStream;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF header stamping failed (primary path). Attempting fallback rebuild.");
                try
                {
                    sourceCopy.Position = 0;
                    var stampedStream = RebuildPdfWithHeader(sourceCopy, metadata, null, cancellationToken);
                    sourceCopy.Dispose();
                    _logger.LogInformation("PDF header stamping fallback succeeded. Output size: {Size} bytes.", stampedStream.Length);
                    return stampedStream;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "PDF header fallback rebuild also failed. Returning original unstamped stream.");
                    try { if (sourceCopy.CanSeek) sourceCopy.Position = 0; } catch { /* swallow */ }
                    return sourceCopy;
                }
            }
        }

        public Task<Stream> CreatePdfFromTextAsync(string textContent, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default)
        {
            try
            {
                using var pdfDocument = new PdfDocument();

                // Define fonts
                var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
                var sectionFont = new XFont("Arial", 11, XFontStyle.Bold);
                var metadataLabelFont = new XFont("Arial", 9.5, XFontStyle.Bold);
                var bodyFont = new XFont("Arial", 9.5, XFontStyle.Regular);
                var bodyBoldFont = new XFont("Arial", 9.5, XFontStyle.Bold);
                var monoFont = new XFont("Courier New", 9, XFontStyle.Regular);

                // Define colors/brushes
                var primaryBrush = new XSolidBrush(XColor.FromArgb(27, 54, 93)); // Navy
                var secondaryBrush = new XSolidBrush(XColor.FromArgb(74, 85, 104)); // Slate Charcoal
                var textBrush = XBrushes.Black;
                var metadataBgBrush = new XSolidBrush(XColor.FromArgb(247, 250, 252)); // Light grey
                var metadataBorderPen = new XPen(XColor.FromArgb(226, 232, 240), 1);

                var logoPath = ResolveLogoPath(metadata.OrganizationLogoPath, metadata.OrganizationCode);
                var logoImage = TryLoadLogoImage(logoPath);
                var signatureImage = TryLoadSignatureImage(metadata.SignatureBase64);

                const double leftMargin = 18d;
                const double rightMargin = 18d;
                const double bottomMargin = 24d;
                const double bodyTopMargin = 14d;

                XGraphics? gfx = null;
                double y = 0d;
                double bodyWidth = 0d;
                double maxY = 0d;

                void StartNewPage()
                {
                    gfx?.Dispose();
                    var page = pdfDocument.AddPage();
                    page.Size = PageSize.A4;
                    gfx = XGraphics.FromPdfPage(page);

                    y = (_enabled && pdfDocument.Pages.Count == 1)
                        ? HeaderTop + HeaderHeight + bodyTopMargin
                        : bodyTopMargin;

                    bodyWidth = page.Width - leftMargin - rightMargin;
                    maxY = page.Height - bottomMargin;
                }

                StartNewPage();

                var normalizedText = NormalizeLineEndings(textContent ?? string.Empty);
                var logicalLines = normalizedText.Split('\n', StringSplitOptions.None);

                // Phase 1: Parse Title, Metadata, and Content
                string? title = null;
                var metadataLines = new List<string>();
                var contentLines = new List<string>();
                bool readingMetadata = true;

                foreach (var rawLine in logicalLines)
                {
                    var line = rawLine.TrimEnd('\r').Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    if (title == null)
                    {
                        title = line;
                        continue;
                    }

                    if (readingMetadata)
                    {
                        // Check if it's metadata (contains colon followed by a value)
                        if (line.Contains(':') && !line.StartsWith('|') && !line.StartsWith('-') && !line.StartsWith('*'))
                        {
                            var parts = line.Split(':', 2);
                            var val = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                            if (!string.IsNullOrEmpty(val))
                            {
                                metadataLines.Add(line);
                                continue;
                            }
                        }
                        
                        // If it's empty value or starts with table/list markers, stop reading metadata
                        readingMetadata = false;
                    }

                    contentLines.Add(rawLine.TrimEnd('\r'));
                }

                // Phase 2: Draw Title Centered
                if (title != null)
                {
                    var titleText = title.ToUpper();
                    var titleSize = gfx!.MeasureString(titleText, titleFont);
                    double titleX = leftMargin + (bodyWidth - titleSize.Width) / 2;
                    gfx.DrawString(titleText, titleFont, primaryBrush, new XPoint(titleX, y));
                    y += 18d;

                    // Draw a subtle horizontal line under the title
                    gfx.DrawLine(new XPen(XColor.FromArgb(27, 54, 93), 1.25), leftMargin + 30, y, leftMargin + bodyWidth - 30, y);
                    y += 20d;
                }

                // Phase 3: Draw Metadata Box
                if (metadataLines.Count > 0)
                {
                    double boxWidth = bodyWidth * 0.85;
                    double boxHeight = (metadataLines.Count * 18d) + 14d;
                    double boxX = leftMargin + (bodyWidth - boxWidth) / 2;

                    if (y + boxHeight > maxY)
                    {
                        StartNewPage();
                        boxX = leftMargin + (bodyWidth - boxWidth) / 2;
                    }

                    var boxRect = new XRect(boxX, y, boxWidth, boxHeight);
                    gfx!.DrawRectangle(metadataBorderPen, metadataBgBrush, boxRect);

                    double itemY = y + 14d;
                    foreach (var metaLine in metadataLines)
                    {
                        var parts = metaLine.Split(':', 2);
                        var key = parts[0].Trim() + " : ";
                        var val = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                        double labelX = boxX + 16d;
                        gfx.DrawString(key, metadataLabelFont, secondaryBrush, new XPoint(labelX, itemY));

                        double keyWidth = gfx.MeasureString(key, metadataLabelFont).Width;
                        double valX = labelX + keyWidth + 4d;
                        gfx.DrawString(val, bodyFont, textBrush, new XPoint(valX, itemY));

                        itemY += 18d;
                    }

                    y += boxHeight + 20d;
                }

                // Phase 4: Draw Content Lines Centered and Formatted
                foreach (var rawLine in contentLines)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        y += 10d; // blank line spacing
                        continue;
                    }

                    // Check if it's a section header (ends with colon)
                    bool isSectionHeader = line.EndsWith(':') && !line.StartsWith('|') && !line.StartsWith('-') && !line.StartsWith('*');
                    
                    if (isSectionHeader)
                    {
                        var headerText = line.ToUpper();
                        var headerSize = gfx!.MeasureString(headerText, sectionFont);
                        double headerX = leftMargin + (bodyWidth - headerSize.Width) / 2;
                        
                        y += 10d; // spacing before header
                        if (y + 20d > maxY)
                        {
                            StartNewPage();
                        }
                        
                        gfx.DrawString(headerText, sectionFont, primaryBrush, new XPoint(headerX, y));
                        y += 18d;
                    }
                    // Check if it's a table line
                    else if (line.StartsWith('|') || line.StartsWith('+') || (line.StartsWith('-') && line.Contains("---")))
                    {
                        var lineSize = gfx!.MeasureString(rawLine, monoFont);
                        double lineX = leftMargin + (bodyWidth - lineSize.Width) / 2;
                        
                        if (y + 14d > maxY)
                        {
                            StartNewPage();
                        }
                        
                        gfx.DrawString(rawLine, monoFont, textBrush, new XPoint(lineX, y));
                        y += 14d;
                    }
                    // Check if it's a list item
                    else if (line.StartsWith('-') || line.StartsWith('*') || (line.Length > 2 && char.IsDigit(line[0]) && line[1] == '.'))
                    {
                        if (y + 16d > maxY)
                        {
                            StartNewPage();
                        }
                        
                        gfx!.DrawString(rawLine, bodyFont, textBrush, new XPoint(leftMargin + 24d, y));
                        y += 16d;
                    }
                    // Regular paragraph text
                    else
                    {
                        var wrappedLines = WrapLine(gfx!, line, bodyFont, bodyWidth * 0.85);
                        foreach (var wrappedLine in wrappedLines)
                        {
                            if (y + 16d > maxY)
                            {
                                StartNewPage();
                            }

                            if (!string.IsNullOrEmpty(wrappedLine))
                            {
                                var wlSize = gfx!.MeasureString(wrappedLine, bodyFont);
                                double wlX = leftMargin + (bodyWidth - wlSize.Width) / 2;
                                gfx.DrawString(wrappedLine, bodyFont, textBrush, new XPoint(wlX, y));
                            }

                            y += 16d;
                        }
                    }
                }

                if (signatureImage != null)
                {
                    if (y + 100 > maxY)
                    {
                        StartNewPage();
                    }
                    DrawSignature(gfx!, pdfDocument.Pages[pdfDocument.Pages.Count - 1].Width, pdfDocument.Pages[pdfDocument.Pages.Count - 1].Height, signatureImage, metadata);
                }

                gfx?.Dispose();
                gfx = null;

                if (_enabled && pdfDocument.Pages.Count > 0)
                {
                    var page = pdfDocument.Pages[0];
                    using var headerGfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    DrawHeader(headerGfx, page.Width, metadata, logoImage, 1, pdfDocument.Pages.Count);

                    pdfDocument.Info.Keywords = AppendStampKeyword(pdfDocument.Info.Keywords);
                }
                logoImage?.Dispose();
                signatureImage?.Dispose();

                var result = new MemoryStream();
                pdfDocument.Save(result, false);
                result.Position = 0;
                return Task.FromResult<Stream>(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TXT to PDF conversion failed. Returning fallback plain PDF.");
                return Task.FromResult<Stream>(CreateFallbackPdf(textContent ?? string.Empty));
            }
        }

        private static MemoryStream CreateFallbackPdf(string textContent)
        {
            using var fallbackDoc = new PdfDocument();
            fallbackDoc.Info.Title = "Document";
            var page = fallbackDoc.AddPage();
            page.Size = PageSize.A4;

            using var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 10, XFontStyle.Regular);
            gfx.DrawString(
                NormalizeLineEndings(textContent),
                font,
                XBrushes.Black,
                new XRect(16, 20, page.Width - 32, page.Height - 40),
                XStringFormats.TopLeft);

            var output = new MemoryStream();
            fallbackDoc.Save(output, false);
            output.Position = 0;
            return output;
        }

        private string? ResolveLogoPath(string? organizationLogoPath, string? organizationCode)
        {
            if (!string.IsNullOrWhiteSpace(organizationLogoPath))
            {
                var normalized = organizationLogoPath.Trim();
                if (Path.IsPathRooted(normalized) && File.Exists(normalized))
                {
                    return normalized;
                }

                var fileName = Path.GetFileName(organizationLogoPath);
                var directFromOrgFolder = !string.IsNullOrWhiteSpace(_organizationLogosPath)
                    ? Path.Combine(_organizationLogosPath, fileName)
                    : null;
                if (!string.IsNullOrWhiteSpace(directFromOrgFolder) && File.Exists(directFromOrgFolder))
                {
                    return directFromOrgFolder;
                }
            }

            if (!string.IsNullOrWhiteSpace(_organizationLogosPath) && !string.IsNullOrWhiteSpace(organizationCode))
            {
                var code = organizationCode.Trim();
                var pngPath = Path.Combine(_organizationLogosPath, $"{code}.png");
                if (File.Exists(pngPath))
                {
                    return pngPath;
                }

                var jpgPath = Path.Combine(_organizationLogosPath, $"{code}.jpg");
                if (File.Exists(jpgPath))
                {
                    return jpgPath;
                }

                var jpegPath = Path.Combine(_organizationLogosPath, $"{code}.jpeg");
                if (File.Exists(jpegPath))
                {
                    return jpegPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(_defaultLogoPath) && File.Exists(_defaultLogoPath))
            {
                return _defaultLogoPath;
            }

            return null;
        }



        private static XImage? TryLoadLogoImage(string? logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
            {
                return null;
            }

            try
            {
                return XImage.FromFile(logoPath);
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
        }

        private static IEnumerable<string> WrapLine(XGraphics gfx, string line, XFont font, double maxWidth)
        {
            if (string.IsNullOrEmpty(line))
            {
                yield return string.Empty;
                yield break;
            }

            var builder = new StringBuilder();
            var words = line.Split(' ', StringSplitOptions.None);

            foreach (var rawWord in words)
            {
                var word = rawWord ?? string.Empty;

                if (word.Length == 0)
                {
                    var spaceCandidate = builder.Length == 0 ? " " : builder + " ";
                    if (gfx.MeasureString(spaceCandidate, font).Width <= maxWidth)
                    {
                        builder.Clear();
                        builder.Append(spaceCandidate);
                    }
                    else
                    {
                        if (builder.Length > 0)
                        {
                            yield return builder.ToString();
                            builder.Clear();
                        }
                    }

                    continue;
                }

                var candidate = builder.Length == 0 ? word : builder + " " + word;
                if (gfx.MeasureString(candidate, font).Width <= maxWidth)
                {
                    builder.Clear();
                    builder.Append(candidate);
                    continue;
                }

                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                if (gfx.MeasureString(word, font).Width <= maxWidth)
                {
                    builder.Append(word);
                    continue;
                }

                foreach (var part in SplitLongWord(gfx, word, font, maxWidth))
                {
                    if (gfx.MeasureString(part, font).Width <= maxWidth)
                    {
                        yield return part;
                    }
                }
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        private static IEnumerable<string> SplitLongWord(XGraphics gfx, string word, XFont font, double maxWidth)
        {
            var start = 0;
            while (start < word.Length)
            {
                var len = 1;
                var bestLen = 1;
                while (start + len <= word.Length)
                {
                    var part = word.Substring(start, len);
                    if (gfx.MeasureString(part, font).Width <= maxWidth)
                    {
                        bestLen = len;
                        len++;
                        continue;
                    }

                    break;
                }

                yield return word.Substring(start, bestLen);
                start += bestLen;
            }
        }

        private static void DrawHeader(XGraphics gfx, double pageWidth, PdfHeaderMetadata metadata, XImage? logoImage, int pageNumber, int totalPages)
        {
            var borderPen   = new XPen(XColors.Black, 0.8);
            var thinPen     = new XPen(XColors.Black, 0.4);
            var fillBrush   = XBrushes.White;
            var textBrush   = XBrushes.Black;
            var labelBrush  = new XSolidBrush(XColor.FromArgb(80, 80, 80));

            // Fonts
            var labelFont     = new XFont("Arial",  7.5, XFontStyle.Regular);
            var valueFont     = new XFont("Arial",  8.5, XFontStyle.Bold);
            var titleFont     = new XFont("Arial", 11.0, XFontStyle.Bold);
            var subInfoFont   = new XFont("Arial",  7.5, XFontStyle.Regular);
            var logoTextFont  = new XFont("Arial",  7.5, XFontStyle.Bold);

            var marginLeft  = 18d;
            var marginRight = 18d;
            var headerY     = HeaderTop;
            var totalWidth  = pageWidth - marginLeft - marginRight;

            // Column widths: left=logo 13%, center=info 55%, right=metadata 32%
            var leftW   = Math.Round(totalWidth * 0.13);
            var rightW  = Math.Round(totalWidth * 0.32);
            var centerW = totalWidth - leftW - rightW;

            var leftX   = marginLeft;
            var centerX = leftX + leftW;
            var rightX  = centerX + centerW;
            var hY      = headerY;
            var hH      = HeaderHeight;

            // ── Background fill ──────────────────────────────────────────────
            gfx.DrawRectangle(fillBrush, new XRect(leftX, hY, totalWidth, hH));

            // ── LEFT COLUMN – Logo ───────────────────────────────────────────
            if (logoImage != null)
            {
                var padding  = 6d;
                var maxLogoW = leftW - padding * 2;
                var maxLogoH = hH    - padding * 2;
                var ratio    = Math.Min(maxLogoW / logoImage.PixelWidth, maxLogoH / logoImage.PixelHeight);
                var drawW    = logoImage.PixelWidth  * ratio;
                var drawH    = logoImage.PixelHeight * ratio;
                var drawX    = leftX + (leftW - drawW) / 2d;
                var drawY    = hY    + (hH   - drawH) / 2d;
                gfx.DrawImage(logoImage, drawX, drawY, drawW, drawH);
            }
            else
            {
                // Fallback: organisation name/code as text
                var orgName = string.IsNullOrWhiteSpace(metadata.OrganizationName)
                    ? "Organisation"
                    : Truncate(metadata.OrganizationName, 14);
                gfx.DrawString(orgName, logoTextFont, textBrush,
                    new XRect(leftX, hY, leftW, hH), XStringFormats.Center);
            }

            // ── CENTER COLUMN ────────────────────────────────────────────────
            // Row 1: Processus & Procédure
            var processCode   = string.IsNullOrWhiteSpace(metadata.ProcessCode)   ? "-" : metadata.ProcessCode.Trim();
            var procedureCode = string.IsNullOrWhiteSpace(metadata.ProcedureCode) ? "-" : metadata.ProcedureCode.Trim();
            var row1H = hH * 0.28;
            var row1Text = $"Processus : {processCode}     |     Procédure : {procedureCode}";
            gfx.DrawString(row1Text, subInfoFont, labelBrush,
                new XRect(centerX + 4, hY, centerW - 8, row1H), XStringFormats.Center);

            // Horizontal divider after row 1
            var div1Y = hY + row1H;
            gfx.DrawLine(thinPen, centerX, div1Y, centerX + centerW, div1Y);

            // Row 2: Document title (large, centred)
            var titleRowH = hH * 0.44;
            var title = string.IsNullOrWhiteSpace(metadata.DocumentTitle)
                ? "Titre du document"
                : Truncate(metadata.DocumentTitle, 55);
            gfx.DrawString(title, titleFont, textBrush,
                new XRect(centerX + 4, div1Y, centerW - 8, titleRowH), XStringFormats.Center);

            // Horizontal divider after row 2
            var div2Y = div1Y + titleRowH;
            gfx.DrawLine(thinPen, centerX, div2Y, centerX + centerW, div2Y);

            // Row 3: Organisation name / description
            var contact = string.IsNullOrWhiteSpace(metadata.OrganizationName) ? "Système de Management Qualité" : metadata.OrganizationName.Trim();
            gfx.DrawString(contact, subInfoFont, labelBrush,
                new XRect(centerX + 4, div2Y, centerW - 8, hH - (div2Y - hY)), XStringFormats.Center);

            // ── RIGHT COLUMN – Metadata grid ────────────────────────────────
            // 5 equal rows: Code | Version | Statut | Date | Page
            var rows = new (string Label, string Value)[]
            {
                ("Code :",    string.IsNullOrWhiteSpace(metadata.DocumentCode)   ? "-" : metadata.DocumentCode.Trim()),
                ("Version :", string.IsNullOrWhiteSpace(metadata.VersionNumber)  ? "-" : metadata.VersionNumber.Trim()),
                ("Statut :",  string.IsNullOrWhiteSpace(metadata.Status)         ? "-" : metadata.Status.Trim()),
                ("Date :",    metadata.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                ("Page :",    $"{pageNumber} / {Math.Max(1, totalPages)}")
            };

            var rowH      = hH / rows.Length;
            var labelColW = rightW * 0.42;
            var valueColW = rightW - labelColW;

            for (var i = 0; i < rows.Length; i++)
            {
                var ry = hY + i * rowH;
                var (rowLabel, rowValue) = rows[i];

                // Row separator (skip first)
                if (i > 0)
                    gfx.DrawLine(thinPen, rightX, ry, rightX + rightW, ry);

                // Internal label/value divider
                var midX = rightX + labelColW;
                gfx.DrawLine(thinPen, midX, ry, midX, ry + rowH);

                // Label text (right-aligned inside label cell)
                gfx.DrawString(rowLabel, labelFont, labelBrush,
                    new XRect(rightX + 2, ry, labelColW - 4, rowH), XStringFormats.CenterLeft);

                // Value text (bold, left-aligned inside value cell)
                gfx.DrawString(rowValue, valueFont, textBrush,
                    new XRect(midX + 3, ry, valueColW - 5, rowH), XStringFormats.CenterLeft);
            }

            // ── Outer border + column dividers ───────────────────────────────
            gfx.DrawRectangle(borderPen, new XRect(leftX, hY, totalWidth, hH));
            gfx.DrawLine(borderPen, centerX,         hY, centerX,         hY + hH);
            gfx.DrawLine(borderPen, rightX,          hY, rightX,          hY + hH);
        }

        private static void DrawSignature(XGraphics gfx, double pageWidth, double pageHeight, XImage signatureImage, PdfHeaderMetadata metadata)
        {
            var labelFont = new XFont("Arial", 9, XFontStyle.Bold);
            var dateFont = new XFont("Arial", 7, XFontStyle.Italic);
            var textBrush = XBrushes.Black;
            var brandBrush = new XSolidBrush(XColor.FromArgb(0, 135, 90));
            var mutedBrush = XBrushes.DimGray;
            var linePen = new XPen(XColor.FromArgb(150, 150, 150), 0.5);
            linePen.DashStyle = XDashStyle.Dash;
            
            double sigWidth = 120;
            double sigHeight = (signatureImage.PixelHeight * sigWidth) / signatureImage.PixelWidth;
            
            if (sigHeight > 70)
            {
                sigHeight = 70;
                sigWidth = (signatureImage.PixelWidth * sigHeight) / signatureImage.PixelHeight;
            }

            double x = pageWidth - sigWidth - 30;
            double y = pageHeight - sigHeight - 50;

            // Signature area decoration
            gfx.DrawString($"Signé par : {metadata.SignerRole ?? string.Empty}", labelFont, brandBrush, new XPoint(x, y - 12));
            gfx.DrawImage(signatureImage, x, y, sigWidth, sigHeight);
            gfx.DrawLine(linePen, x, y + sigHeight + 5, x + sigWidth, y + sigHeight + 5);
            
            gfx.DrawString($"Date de signature: {DateTime.Now.ToString("dd/MM/yyyy")}", dateFont, mutedBrush, new XPoint(x, y + sigHeight + 15));
        }

        private static XImage? TryLoadSignatureImage(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return null;
            }

            try
            {
                var data = base64;
                if (data.Contains(","))
                {
                    data = data.Split(',')[1];
                }

                var bytes = Convert.FromBase64String(data);
                var ms = new MemoryStream(bytes);
                return XImage.FromStream(() => ms);
            }
            catch
            {
                return null;
            }
        }

        private static bool LooksLikePdf(MemoryStream stream)
        {
            if (stream.Length < 4)
            {
                return false;
            }

            var pos = stream.Position;
            try
            {
                stream.Position = 0;
                var b0 = stream.ReadByte();
                var b1 = stream.ReadByte();
                var b2 = stream.ReadByte();
                var b3 = stream.ReadByte();
                return b0 == '%' && b1 == 'P' && b2 == 'D' && b3 == 'F';
            }
            catch
            {
                return false;
            }
            finally
            {
                try { stream.Position = pos; } catch { /* swallow */ }
            }
        }

        private MemoryStream RebuildPdfWithHeader(
            MemoryStream sourcePdf,
            PdfHeaderMetadata metadata,
            string? sourceKeywords,
            CancellationToken cancellationToken)
        {
            sourcePdf.Position = 0;
            using var form = XPdfForm.FromStream(sourcePdf);
            using var outputDocument = new PdfDocument();
            var logoPath = ResolveLogoPath(metadata.OrganizationLogoPath, metadata.OrganizationCode);
            var logoImage = TryLoadLogoImage(logoPath);
            var signatureImage = TryLoadSignatureImage(metadata.SignatureBase64);

            _logger.LogInformation(
                "RebuildPdfWithHeader: pages={Pages}, logoPath={Logo}, hasSignature={Sig}",
                form.PageCount, logoPath ?? "(none)", signatureImage != null);

            for (var index = 0; index < form.PageCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                form.PageNumber = index + 1;
                var page = outputDocument.AddPage();
                var originalWidth = form.PointWidth;
                var originalHeight = form.PointHeight;

                if (index == 0)
                {
                    // Draw first page with expanded height and the header
                    var headerSpace = HeaderTop + HeaderHeight + 4d;
                    page.Width = originalWidth;
                    page.Height = originalHeight + headerSpace;

                    using var gfx = XGraphics.FromPdfPage(page);
                    DrawPageWithHeader(gfx, page.Width, page.Height, originalHeight, form, metadata, logoImage, index + 1, form.PageCount);

                    if (index == form.PageCount - 1 && signatureImage != null)
                    {
                        DrawSignature(gfx, page.Width, page.Height, signatureImage, metadata);
                    }
                }
                else
                {
                    // Keep other pages at original height with no header
                    page.Width = originalWidth;
                    page.Height = originalHeight;

                    using var gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(form, 0, 0, page.Width, page.Height);

                    if (index == form.PageCount - 1 && signatureImage != null)
                    {
                        DrawSignature(gfx, page.Width, page.Height, signatureImage, metadata);
                    }
                }
            }

            logoImage?.Dispose();
            signatureImage?.Dispose();
            outputDocument.Info.Keywords = AppendStampKeyword(sourceKeywords);

            var stampedStream = new MemoryStream();
            outputDocument.Save(stampedStream, false);
            stampedStream.Position = 0;
            return stampedStream;
        }

        private static void DrawPageWithHeader(
            XGraphics gfx,
            double pageWidth,
            double pageHeight,
            double sourceContentHeight,
            XPdfForm sourcePage,
            PdfHeaderMetadata metadata,
            XImage? logoImage,
            int pageNumber,
            int totalPages)
        {
            const double contentGap = 4d;
            var contentTop = HeaderTop + HeaderHeight + contentGap;

            // Draw the header block at the top
            DrawHeader(gfx, pageWidth, metadata, logoImage, pageNumber, totalPages);

            // Draw the original page content at its original size below the header
            gfx.DrawImage(sourcePage, 0, contentTop, pageWidth, sourceContentHeight);
        }

        private static bool IsAlreadyStamped(PdfDocument pdfDocument)
        {
            var keywords = pdfDocument.Info.Keywords;
            return !string.IsNullOrWhiteSpace(keywords) &&
                keywords.Contains(HeaderStampKeyword, StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendStampKeyword(string? keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
            {
                return HeaderStampKeyword;
            }

            if (keywords.Contains(HeaderStampKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return keywords;
            }

            return $"{keywords};{HeaderStampKeyword}";
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized[..maxLength] + "...";
        }

        private static async Task<MemoryStream> CopyToMemoryAsync(Stream source, CancellationToken cancellationToken)
        {
            var memoryStream = new MemoryStream();
            if (source.CanSeek)
            {
                source.Position = 0;
            }

            await source.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }

        private static string? ResolveOptionalPath(string? configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return null;
            }

            var trimmed = configuredPath.Trim();
            if (Path.IsPathRooted(trimmed))
            {
                return trimmed;
            }

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), trimmed));
        }

        private const double HeaderTop    = 10d;
        private const double HeaderHeight  = 90d;
    }
}

