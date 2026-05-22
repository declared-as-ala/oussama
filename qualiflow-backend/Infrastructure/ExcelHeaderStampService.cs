using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocApi.Infrastructure
{
    public sealed class ExcelHeaderStampService : IExcelHeaderStampService
    {
        private const uint HeaderRowCount = 7;
        private const string HeaderMarker = "QualiFlow - Fiche document";
        private const string LegacyHeaderMarker = "En-tete QualiFlow";
        private readonly bool _enabled;
        private readonly ILogger<ExcelHeaderStampService> _logger;

        public ExcelHeaderStampService(IConfiguration configuration, ILogger<ExcelHeaderStampService> logger)
        {
            _enabled = configuration.GetValue("Storage:ExcelHeaderEnabled", true);
            _logger = logger;
        }

        public Task ApplyWorkbookHeaderAsync(string absoluteXlsxPath, PdfHeaderMetadata metadata, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            if (!_enabled ||
                string.IsNullOrWhiteSpace(absoluteXlsxPath) ||
                !File.Exists(absoluteXlsxPath) ||
                !absoluteXlsxPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            try
            {
                using var document = SpreadsheetDocument.Open(absoluteXlsxPath, true);
                var workbookPart = document.WorkbookPart;
                var firstSheet = workbookPart?.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault();
                if (workbookPart == null || firstSheet?.Id?.Value == null)
                {
                    return Task.CompletedTask;
                }

                var worksheetPart = workbookPart.GetPartById(firstSheet.Id.Value) as WorksheetPart;
                if (worksheetPart?.Worksheet == null)
                {
                    return Task.CompletedTask;
                }

                var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
                if (sheetData == null)
                {
                    sheetData = new SheetData();
                    worksheetPart.Worksheet.Append(sheetData);
                }

                RemoveExistingHeader(sheetData);
                ShiftRows(sheetData, (int)HeaderRowCount);
                var styles = EnsureHeaderStyles(workbookPart);

                foreach (var row in BuildHeaderRows(metadata, styles).Reverse())
                {
                    sheetData.PrependChild(row);
                }

                worksheetPart.Worksheet.Save();
                workbookPart.Workbook.Save();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Excel header injection failed for {Path}. Original file kept.", absoluteXlsxPath);
            }

            return Task.CompletedTask;
        }

        private static void RemoveExistingHeader(SheetData sheetData)
        {
            var firstCellText = sheetData.Elements<Row>()
                .FirstOrDefault(r => r.RowIndex?.Value == 1)?
                .Elements<Cell>()
                .FirstOrDefault(c => string.Equals(GetColumnName(c.CellReference?.Value), "A", StringComparison.OrdinalIgnoreCase))?
                .InnerText;

            if (!string.Equals(firstCellText, HeaderMarker, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(firstCellText, LegacyHeaderMarker, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var headerRows = sheetData.Elements<Row>()
                .Where(r => r.RowIndex?.Value <= HeaderRowCount)
                .ToList();

            foreach (var row in headerRows)
            {
                row.Remove();
            }

            ShiftRows(sheetData, -(int)HeaderRowCount);
        }

        private static void ShiftRows(SheetData sheetData, int offset)
        {
            if (offset == 0)
            {
                return;
            }

            foreach (var row in sheetData.Elements<Row>().OrderByDescending(r => r.RowIndex?.Value ?? 0))
            {
                var currentIndex = (int)(row.RowIndex?.Value ?? 0);
                if (currentIndex <= 0)
                {
                    continue;
                }

                var newIndex = Math.Max(1, currentIndex + offset);
                row.RowIndex = (uint)newIndex;

                foreach (var cell in row.Elements<Cell>())
                {
                    UpdateCellReference(cell, (uint)newIndex);
                }
            }
        }

        private static Row[] BuildHeaderRows(PdfHeaderMetadata metadata, ExcelHeaderStyles styles)
        {
            var org = string.IsNullOrWhiteSpace(metadata.OrganizationName) ? "Organisation" : metadata.OrganizationName.Trim();
            var document = string.IsNullOrWhiteSpace(metadata.DocumentTitle)
                ? metadata.DocumentCode
                : $"{metadata.DocumentCode} - {metadata.DocumentTitle}".Trim(' ', '-');
            var version = string.IsNullOrWhiteSpace(metadata.VersionNumber) ? "-" : metadata.VersionNumber.Trim();
            var status = string.IsNullOrWhiteSpace(metadata.Status) ? "-" : metadata.Status.Trim();
            var process = string.IsNullOrWhiteSpace(metadata.ProcessCode) ? "-" : metadata.ProcessCode.Trim();
            var procedure = string.IsNullOrWhiteSpace(metadata.ProcedureCode) ? "-" : metadata.ProcedureCode.Trim();
            var generated = metadata.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            return new[]
            {
                CreateRow(1, 26d, ("A", HeaderMarker, styles.Title), ("B", document, styles.Title), ("C", "Version", styles.Title), ("D", version, styles.Title)),
                CreateRow(2, 22d, ("A", "Organisation", styles.Label), ("B", org, styles.Value), ("C", "Statut", styles.Label), ("D", status, styles.Value)),
                CreateRow(3, 22d, ("A", "Document", styles.Label), ("B", document, styles.Value), ("C", "Genere le", styles.Label), ("D", generated, styles.Value)),
                CreateRow(4, 22d, ("A", "Processus", styles.Label), ("B", process, styles.Value), ("C", "Procedure", styles.Label), ("D", procedure, styles.Value)),
                CreateRow(5, 6d, ("A", string.Empty, styles.Spacer), ("B", string.Empty, styles.Spacer), ("C", string.Empty, styles.Spacer), ("D", string.Empty, styles.Spacer)),
                CreateRow(6, 6d, ("A", string.Empty, styles.Spacer), ("B", string.Empty, styles.Spacer), ("C", string.Empty, styles.Spacer), ("D", string.Empty, styles.Spacer)),
                CreateRow(7, 4d, ("A", string.Empty, styles.Spacer), ("B", string.Empty, styles.Spacer), ("C", string.Empty, styles.Spacer), ("D", string.Empty, styles.Spacer))
            };
        }

        private static Row CreateRow(uint index, double? height, params (string Column, string Value, uint StyleIndex)[] cells)
        {
            var row = new Row { RowIndex = index };
            if (height.HasValue)
            {
                row.Height = height.Value;
                row.CustomHeight = true;
            }

            foreach (var (column, value, styleIndex) in cells)
            {
                row.Append(CreateTextCell(column, index, value, styleIndex));
            }

            return row;
        }

        private static Cell CreateTextCell(string column, uint rowIndex, string value, uint styleIndex)
        {
            return new Cell
            {
                CellReference = $"{column}{rowIndex}",
                DataType = CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString = new InlineString(new Text(value ?? string.Empty))
            };
        }

        private static ExcelHeaderStyles EnsureHeaderStyles(WorkbookPart workbookPart)
        {
            var stylesPart = workbookPart.WorkbookStylesPart ?? workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet ??= CreateDefaultStylesheet();

            var stylesheet = stylesPart.Stylesheet;
            var fonts = stylesheet.Fonts ?? stylesheet.AppendChild(new Fonts { Count = 0U });
            var fills = stylesheet.Fills ?? stylesheet.AppendChild(new Fills { Count = 0U });
            var borders = stylesheet.Borders ?? stylesheet.AppendChild(new Borders { Count = 0U });
            var cellFormats = stylesheet.CellFormats ?? stylesheet.AppendChild(new CellFormats { Count = 0U });

            var titleFontId = AppendFont(fonts, bold: true, color: "FFFFFF", size: 12d);
            var labelFontId = AppendFont(fonts, bold: true, color: "374151", size: 10d);
            var valueFontId = AppendFont(fonts, bold: false, color: "111827", size: 10d);

            var titleFillId = AppendFill(fills, "167D5C");
            var labelFillId = AppendFill(fills, "E8F5EF");
            var valueFillId = AppendFill(fills, "FFFFFF");
            var spacerFillId = AppendFill(fills, "F9FAFB");
            var borderId = AppendBorder(borders);

            var titleStyle = AppendCellFormat(cellFormats, titleFontId, titleFillId, borderId, horizontal: HorizontalAlignmentValues.Center);
            var labelStyle = AppendCellFormat(cellFormats, labelFontId, labelFillId, borderId);
            var valueStyle = AppendCellFormat(cellFormats, valueFontId, valueFillId, borderId);
            var spacerStyle = AppendCellFormat(cellFormats, valueFontId, spacerFillId, borderId);

            stylesPart.Stylesheet.Save();
            return new ExcelHeaderStyles(titleStyle, labelStyle, valueStyle, spacerStyle);
        }

        private static Stylesheet CreateDefaultStylesheet()
        {
            return new Stylesheet(
                new Fonts(new Font()) { Count = 1U },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
                { Count = 2U },
                new Borders(new Border()) { Count = 1U },
                new CellStyleFormats(new CellFormat()) { Count = 1U },
                new CellFormats(new CellFormat()) { Count = 1U },
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U }) { Count = 1U },
                new DifferentialFormats { Count = 0U },
                new TableStyles { Count = 0U, DefaultTableStyle = "TableStyleMedium2", DefaultPivotStyle = "PivotStyleLight16" });
        }

        private static uint AppendFont(Fonts fonts, bool bold, string color, double size)
        {
            var font = new Font(
                new FontSize { Val = size },
                new Color { Rgb = new HexBinaryValue(color) },
                new FontName { Val = "Calibri" });

            if (bold)
            {
                font.InsertAt(new Bold(), 0);
            }

            fonts.Append(font);
            fonts.Count = (uint)fonts.ChildElements.Count;
            return fonts.Count.Value - 1;
        }

        private static uint AppendFill(Fills fills, string color)
        {
            fills.Append(new Fill(
                new PatternFill(
                    new ForegroundColor { Rgb = new HexBinaryValue(color) },
                    new BackgroundColor { Indexed = 64U })
                { PatternType = PatternValues.Solid }));
            fills.Count = (uint)fills.ChildElements.Count;
            return fills.Count.Value - 1;
        }

        private static uint AppendBorder(Borders borders)
        {
            borders.Append(new Border(
                new LeftBorder(CreateBorderColor()) { Style = BorderStyleValues.Thin },
                new RightBorder(CreateBorderColor()) { Style = BorderStyleValues.Thin },
                new TopBorder(CreateBorderColor()) { Style = BorderStyleValues.Thin },
                new BottomBorder(CreateBorderColor()) { Style = BorderStyleValues.Thin },
                new DiagonalBorder()));
            borders.Count = (uint)borders.ChildElements.Count;
            return borders.Count.Value - 1;
        }

        private static Color CreateBorderColor() => new() { Rgb = new HexBinaryValue("D1D5DB") };

        private static uint AppendCellFormat(
            CellFormats cellFormats,
            uint fontId,
            uint fillId,
            uint borderId,
            HorizontalAlignmentValues? horizontal = null)
        {
            cellFormats.Append(new CellFormat
            {
                FontId = fontId,
                FillId = fillId,
                BorderId = borderId,
                ApplyFont = true,
                ApplyFill = true,
                ApplyBorder = true,
                ApplyAlignment = true,
                Alignment = new Alignment
                {
                    Horizontal = horizontal ?? HorizontalAlignmentValues.Left,
                    Vertical = VerticalAlignmentValues.Center,
                    WrapText = true
                }
            });
            cellFormats.Count = (uint)cellFormats.ChildElements.Count;
            return cellFormats.Count.Value - 1;
        }

        private static void UpdateCellReference(Cell cell, uint rowIndex)
        {
            var reference = cell.CellReference?.Value;
            if (string.IsNullOrWhiteSpace(reference))
            {
                return;
            }

            var column = GetColumnName(reference);
            if (string.IsNullOrWhiteSpace(column))
            {
                return;
            }

            cell.CellReference = $"{column}{rowIndex}";
        }

        private static string GetColumnName(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return string.Empty;
            }

            return new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        }

        private sealed record ExcelHeaderStyles(uint Title, uint Label, uint Value, uint Spacer);
    }
}
