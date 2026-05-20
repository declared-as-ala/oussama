using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocApi.Infrastructure
{
    public sealed class WordHeaderStampService : IWordHeaderStampService
    {
        private readonly bool _enabled;
        private readonly ILogger<WordHeaderStampService> _logger;

        public WordHeaderStampService(IConfiguration configuration, ILogger<WordHeaderStampService> logger)
        {
            _enabled = configuration.GetValue("Storage:WordHeaderEnabled", true);
            _logger = logger;
        }

        public Task ApplyFirstPageHeaderAsync(string absoluteDocxPath, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            if (!_enabled || string.IsNullOrWhiteSpace(absoluteDocxPath) || !File.Exists(absoluteDocxPath))
            {
                return Task.CompletedTask;
            }

            try
            {
                using var wordDoc = WordprocessingDocument.Open(absoluteDocxPath, true);
                var mainPart = wordDoc.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                {
                    return Task.CompletedTask;
                }

                var sectionProps = mainPart.Document.Body.Elements<SectionProperties>().LastOrDefault();
                if (sectionProps == null)
                {
                    sectionProps = new SectionProperties();
                    mainPart.Document.Body.Append(sectionProps);
                }

                sectionProps.RemoveAllChildren<TitlePage>();
                sectionProps.PrependChild(new TitlePage());

                var oldFirstHeader = sectionProps.Elements<HeaderReference>()
                    .FirstOrDefault(h => h.Type != null && h.Type.Value == HeaderFooterValues.First);
                oldFirstHeader?.Remove();

                var headerPart = mainPart.AddNewPart<HeaderPart>();
                headerPart.Header = new Header(
                    BuildHeaderTable(metadata),
                    new Paragraph(
                        new ParagraphProperties(
                            new ParagraphStyleId { Val = "Header" },
                            new SpacingBetweenLines { Before = "0", After = "0", Line = "180", LineRule = LineSpacingRuleValues.Auto }),
                        new Run(new RunProperties(new NoProof()), new Text(string.Empty))));
                headerPart.Header.Save();

                var headerRef = new HeaderReference
                {
                    Type = HeaderFooterValues.First,
                    Id = mainPart.GetIdOfPart(headerPart)
                };
                sectionProps.Append(headerRef);

                mainPart.Document.Save();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Word first-page header injection failed for {Path}. Original file kept.", absoluteDocxPath);
            }

            return Task.CompletedTask;
        }

        private static Table BuildHeaderTable(PdfHeaderMetadata metadata)
        {
            var org = string.IsNullOrWhiteSpace(metadata.OrganizationName) ? "Organisation" : metadata.OrganizationName.Trim();
            var code = string.IsNullOrWhiteSpace(metadata.DocumentCode) ? "-" : metadata.DocumentCode.Trim();
            var version = string.IsNullOrWhiteSpace(metadata.VersionNumber) ? "-" : metadata.VersionNumber.Trim();
            var process = string.IsNullOrWhiteSpace(metadata.ProcessCode) ? "-" : metadata.ProcessCode.Trim();
            var procedure = string.IsNullOrWhiteSpace(metadata.ProcedureCode) ? "-" : metadata.ProcedureCode.Trim();
            var generated = metadata.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            var table = new Table(
                new TableProperties(
                    new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 8U, Color = "D1D5DB" },
                        new BottomBorder { Val = BorderValues.Single, Size = 8U, Color = "D1D5DB" },
                        new LeftBorder { Val = BorderValues.Single, Size = 8U, Color = "D1D5DB" },
                        new RightBorder { Val = BorderValues.Single, Size = 8U, Color = "D1D5DB" },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 8U, Color = "E5E7EB" },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 8U, Color = "E5E7EB" })),
                new TableGrid(
                    new GridColumn { Width = "1440" },
                    new GridColumn { Width = "1440" },
                    new GridColumn { Width = "1440" },
                    new GridColumn { Width = "1440" },
                    new GridColumn { Width = "1440" },
                    new GridColumn { Width = "1440" }));

            var labels = new TableRow(
                CreateHeaderCell("Organisation"),
                CreateHeaderCell("Document"),
                CreateHeaderCell("Version"),
                CreateHeaderCell("Processus"),
                CreateHeaderCell("Procedure"),
                CreateHeaderCell("Genere le"));

            var values = new TableRow(
                CreateValueCell(org),
                CreateValueCell(code),
                CreateValueCell(version),
                CreateValueCell(process),
                CreateValueCell(procedure),
                CreateValueCell(generated));

            table.Append(labels, values);
            return table;
        }

        private static TableCell CreateHeaderCell(string text)
        {
            return new TableCell(
                new TableCellProperties(
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                    new Shading { Fill = "F9FAFB", Val = ShadingPatternValues.Clear }),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new SpacingBetweenLines { Before = "0", After = "0", Line = "200", LineRule = LineSpacingRuleValues.Auto }),
                    new Run(
                        new RunProperties(
                            new Bold(),
                            new Color { Val = "374151" },
                            new FontSize { Val = "16" },
                            new NoProof()),
                        new Text(text))));
        }

        private static TableCell CreateValueCell(string text)
        {
            return new TableCell(
                new TableCellProperties(
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
                new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new SpacingBetweenLines { Before = "0", After = "0", Line = "220", LineRule = LineSpacingRuleValues.Auto }),
                    new Run(
                        new RunProperties(
                            new Color { Val = "111827" },
                            new FontSize { Val = "18" },
                            new NoProof()),
                        new Text(text))));
        }
    }
}
