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
        private const string HeaderStampKeyword = "QualiFlowHeaderStamped";
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

            if (!_enabled || !LooksLikePdf(sourceCopy))
            {
                sourceCopy.Position = 0;
                return sourceCopy;
            }

            try
            {
                sourceCopy.Position = 0;
                using var pdfDocument = PdfReader.Open(sourceCopy, PdfDocumentOpenMode.Modify);
                if (IsAlreadyStamped(pdfDocument))
                {
                    return new MemoryStream(sourceCopy.ToArray());
                }

                var logoPath = ResolveLogoPath(metadata.OrganizationLogoPath, metadata.OrganizationCode);
                var logoImage = TryLoadLogoImage(logoPath);
                var signatureImage = TryLoadSignatureImage(metadata.SignatureBase64);

                for (int i = 0; i < pdfDocument.Pages.Count; i++)
                {
                    var page = pdfDocument.Pages[i];
                    cancellationToken.ThrowIfCancellationRequested();
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    if (i == 0) // Draw header on the first page only!
                    {
                        DrawHeader(gfx, page.Width, metadata, logoImage, i + 1, pdfDocument.Pages.Count);
                    }

                    // Draw signature on the last page bottom
                    if (i == pdfDocument.Pages.Count - 1 && signatureImage != null)
                    {
                        DrawSignature(gfx, page.Width, page.Height, signatureImage, metadata);
                    }
                }

                logoImage?.Dispose();
                signatureImage?.Dispose();
                pdfDocument.Info.Keywords = AppendStampKeyword(pdfDocument.Info.Keywords);

                var stampedStream = new MemoryStream();
                pdfDocument.Save(stampedStream, false);
                stampedStream.Position = 0;

                sourceCopy.Dispose();
                return stampedStream;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF header stamping failed. Returning original stream.");
                sourceCopy.Position = 0;
                return sourceCopy;
            }
        }

        public Task<Stream> CreatePdfFromTextAsync(string textContent, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default)
        {
            try
            {
                using var pdfDocument = new PdfDocument();
                var bodyFont = new XFont("Arial", 10, XFontStyle.Regular);
                var textBrush = XBrushes.Black;
                var logoPath = ResolveLogoPath(metadata.OrganizationLogoPath, metadata.OrganizationCode);
                var logoImage = TryLoadLogoImage(logoPath);
                var signatureImage = TryLoadSignatureImage(metadata.SignatureBase64);

                const double leftMargin = 18d;
                const double rightMargin = 18d;
                const double bottomMargin = 24d;
                const double lineHeight = 16d;
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

                    if (_enabled && pdfDocument.PageCount == 1) // First page only!
                    {
                        y = HeaderHeight + bodyTopMargin;
                    }
                    else
                    {
                        y = bodyTopMargin;
                    }

                    bodyWidth = page.Width - leftMargin - rightMargin;
                    maxY = page.Height - bottomMargin;
                }

                StartNewPage();

                var normalizedText = NormalizeLineEndings(textContent ?? string.Empty);
                var logicalLines = normalizedText.Split('\n', StringSplitOptions.None);

                foreach (var rawLine in logicalLines)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var line = rawLine.TrimEnd('\r');
                    var wrappedLines = WrapLine(gfx!, line, bodyFont, bodyWidth);
                    foreach (var wrappedLine in wrappedLines)
                    {
                        if (y + lineHeight > maxY)
                        {
                            StartNewPage();
                        }

                        if (!string.IsNullOrEmpty(wrappedLine))
                        {
                            gfx!.DrawString(wrappedLine, bodyFont, textBrush, new XPoint(leftMargin, y));
                        }

                        y += lineHeight;
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

                if (_enabled)
                {
                    for (int i = 0; i < pdfDocument.Pages.Count; i++)
                    {
                        if (i == 0) // Draw header on the first page only!
                        {
                            var page = pdfDocument.Pages[i];
                            using var headerGfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                            DrawHeader(headerGfx, page.Width, metadata, logoImage, i + 1, pdfDocument.Pages.Count);
                        }
                    }

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

        private static string BuildOrganizationContact(PdfHeaderMetadata metadata)
        {
            var email = string.IsNullOrWhiteSpace(metadata.OrganizationEmail) ? null : metadata.OrganizationEmail.Trim();
            var phone = string.IsNullOrWhiteSpace(metadata.OrganizationPhone) ? null : metadata.OrganizationPhone.Trim();

            if (email == null && phone == null)
            {
                return string.Empty;
            }

            if (email != null && phone != null)
            {
                return $"{email} | {phone}";
            }

            return email ?? phone ?? string.Empty;
        }

        private static XImage? TryLoadLogoImage(string? logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
            {
                return null;
            }

            return XImage.FromFile(logoPath);
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
            var borderPen = new XPen(XColors.Black, 0.8);
            var linePen = new XPen(XColors.Black, 0.6);
            var fillBrush = XBrushes.White;

            var centerTopFont = new XFont("Arial", 12, XFontStyle.Bold);
            var titleFont = new XFont("Arial", 18, XFontStyle.Bold);
            var rightCodeFont = new XFont("Arial", 14, XFontStyle.Bold);
            var rightInfoFont = new XFont("Arial", 10, XFontStyle.Bold);
            var textBrush = XBrushes.Black;

            var marginLeft = 6d;
            var marginRight = 6d;
            var headerY = 6d;
            var headerWidth = pageWidth - marginLeft - marginRight;
            var headerRect = new XRect(marginLeft, headerY, headerWidth, HeaderHeight);

            var leftWidth = 104d;
            var rightWidth = 108d;
            var centerWidth = headerWidth - leftWidth - rightWidth;

            var leftRect = new XRect(headerRect.X, headerRect.Y, leftWidth, HeaderHeight);
            var centerRect = new XRect(leftRect.Right, headerRect.Y, centerWidth, HeaderHeight);
            var rightRect = new XRect(centerRect.Right, headerRect.Y, rightWidth, HeaderHeight);

            gfx.DrawRectangle(fillBrush, headerRect);
            gfx.DrawRectangle(borderPen, headerRect);
            gfx.DrawLine(borderPen, leftRect.Right, headerRect.Y, leftRect.Right, headerRect.Bottom);
            gfx.DrawLine(borderPen, rightRect.X, headerRect.Y, rightRect.X, headerRect.Bottom);

            if (logoImage != null)
            {
                var maxLogoW = leftRect.Width - 10;
                var maxLogoH = leftRect.Height - 10;
                var ratio = Math.Min(maxLogoW / logoImage.PixelWidth, maxLogoH / logoImage.PixelHeight);
                var drawW = logoImage.PixelWidth * ratio;
                var drawH = logoImage.PixelHeight * ratio;
                var drawX = leftRect.X + (leftRect.Width - drawW) / 2d;
                var drawY = leftRect.Y + (leftRect.Height - drawH) / 2d;
                gfx.DrawImage(logoImage, drawX, drawY, drawW, drawH);
            }

            var processCode = string.IsNullOrWhiteSpace(metadata.ProcessCode) ? "-" : metadata.ProcessCode.Trim();
            var procedureCode = string.IsNullOrWhiteSpace(metadata.ProcedureCode) ? "-" : metadata.ProcedureCode.Trim();
            var centerTopText = $"Processus : {processCode}   ---   Procedure : {procedureCode}";
            gfx.DrawString(centerTopText, centerTopFont, textBrush, new XRect(centerRect.X, centerRect.Y + 8, centerRect.Width, 22), XStringFormats.TopCenter);
            gfx.DrawLine(linePen, centerRect.X + 6, centerRect.Y + 28, centerRect.Right - 6, centerRect.Y + 28);

            var title = string.IsNullOrWhiteSpace(metadata.DocumentTitle) ? "Titre de document" : Truncate(metadata.DocumentTitle, 48);
            gfx.DrawString(title, titleFont, textBrush, new XRect(centerRect.X, centerRect.Y + 30, centerRect.Width, 34), XStringFormats.TopCenter);

            var dateText = metadata.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var docCode = string.IsNullOrWhiteSpace(metadata.DocumentCode) ? "-" : metadata.DocumentCode.Trim();
            var version = string.IsNullOrWhiteSpace(metadata.VersionNumber) ? "-" : metadata.VersionNumber.Trim();
            var pageText = $"{pageNumber} / {Math.Max(1, totalPages)}";

            gfx.DrawString(docCode, rightCodeFont, textBrush, new XRect(rightRect.X + 4, rightRect.Y + 6, rightRect.Width - 8, 20), XStringFormats.TopCenter);
            gfx.DrawString($"Version : {version}", rightInfoFont, textBrush, new XRect(rightRect.X + 6, rightRect.Y + 26, rightRect.Width - 12, 14), XStringFormats.TopLeft);
            gfx.DrawString($"Date : {dateText}", rightInfoFont, textBrush, new XRect(rightRect.X + 6, rightRect.Y + 42, rightRect.Width - 12, 14), XStringFormats.TopLeft);
            gfx.DrawString($"Page : {pageText}", rightInfoFont, textBrush, new XRect(rightRect.X + 6, rightRect.Y + 58, rightRect.Width - 12, 14), XStringFormats.TopLeft);
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

            var buffer = stream.GetBuffer();
            return buffer[0] == '%' && buffer[1] == 'P' && buffer[2] == 'D' && buffer[3] == 'F';
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

        private const double HeaderHeight = 78d;
    }
}

