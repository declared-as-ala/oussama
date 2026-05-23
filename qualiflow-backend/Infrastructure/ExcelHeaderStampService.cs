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
        // ── Layout constants ─────────────────────────────────────────────────
        private const uint   HeaderRowCount   = 5;
        private const string HeaderMarker     = "QualiFlow - Fiche document V2";
        private const string LegacyMarker1    = "QualiFlow - Fiche document";
        private const string LegacyMarker2    = "En-tete QualiFlow";

        // Column widths in character units (Excel column width unit)
        private const double ColA = 22d;   // label column
        private const double ColB = 38d;   // value column (wide)
        private const double ColC = 18d;   // right label column
        private const double ColD = 28d;   // right value column

        private readonly bool   _enabled;
        private readonly ILogger<ExcelHeaderStampService> _logger;

        public ExcelHeaderStampService(IConfiguration configuration, ILogger<ExcelHeaderStampService> logger)
        {
            _enabled = configuration.GetValue("Storage:ExcelHeaderEnabled", true);
            _logger  = logger;
        }

        // ── Public entry point ────────────────────────────────────────────────
        public Task ApplyWorkbookHeaderAsync(
            string absoluteXlsxPath,
            PdfHeaderMetadata metadata,
            CancellationToken cancellationToken = default)
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
                using var document    = SpreadsheetDocument.Open(absoluteXlsxPath, true);
                var workbookPart      = document.WorkbookPart;
                var firstSheet        = workbookPart?.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault();
                if (workbookPart == null || firstSheet?.Id?.Value == null)
                    return Task.CompletedTask;

                var worksheetPart = workbookPart.GetPartById(firstSheet.Id.Value) as WorksheetPart;
                if (worksheetPart?.Worksheet == null)
                    return Task.CompletedTask;

                var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
                if (sheetData == null)
                {
                    sheetData = new SheetData();
                    worksheetPart.Worksheet.Append(sheetData);
                }

                // Remove any previous header (idempotent)
                RemoveExistingHeader(sheetData);

                // Push existing rows down to make room
                ShiftRows(sheetData, (int)HeaderRowCount);

                // Build & inject header rows
                var styles = EnsureHeaderStyles(workbookPart);
                foreach (var row in BuildHeaderRows(metadata, styles).Reverse())
                    sheetData.PrependChild(row);

                // Fix column widths and freeze the header
                ApplyColumnWidths(worksheetPart);
                ApplyFreezePane(worksheetPart);

                worksheetPart.Worksheet.Save();
                workbookPart.Workbook.Save();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Excel header injection failed for {Path}. Original file kept.", absoluteXlsxPath);
            }

            return Task.CompletedTask;
        }

        // ── Header rows ───────────────────────────────────────────────────────
        private static Row[] BuildHeaderRows(PdfHeaderMetadata metadata, ExcelHeaderStyles styles)
        {
            var org       = Trim(metadata.OrganizationName,  "Organisation");
            var docLabel  = BuildDocLabel(metadata);
            var version   = Trim(metadata.VersionNumber,     "-");
            var status    = Trim(metadata.Status,            "-");
            var process   = Trim(metadata.ProcessCode,       "-");
            var procedure = Trim(metadata.ProcedureCode,     "-");
            var generated = metadata.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            return new[]
            {
                // Row 1 — title banner
                //   A: marker (hidden label so idempotency check works)
                //   B: document title
                //   C: "Version"
                //   D: version value
                CreateRow(1, 28d,
                    ("A", HeaderMarker, styles.TitleLabel),
                    ("B", docLabel,     styles.TitleValue),
                    ("C", "Version",    styles.TitleLabel),
                    ("D", version,      styles.TitleValue)),

                // Row 2
                CreateRow(2, 22d,
                    ("A", "Organisation", styles.Label),
                    ("B", org,            styles.Value),
                    ("C", "Statut",       styles.Label),
                    ("D", status,         styles.Value)),

                // Row 3
                CreateRow(3, 22d,
                    ("A", "Processus",  styles.Label),
                    ("B", process,      styles.Value),
                    ("C", "Procédure",  styles.Label),
                    ("D", procedure,    styles.Value)),

                // Row 4
                CreateRow(4, 22d,
                    ("A", "Généré le",  styles.Label),
                    ("B", generated,    styles.Value),
                    ("C", "Document",   styles.Label),
                    ("D", docLabel,     styles.Value)),

                // Row 5 — thin visual separator
                CreateRow(5, 5d,
                    ("A", string.Empty, styles.Spacer),
                    ("B", string.Empty, styles.Spacer),
                    ("C", string.Empty, styles.Spacer),
                    ("D", string.Empty, styles.Spacer)),
            };
        }

        private static string BuildDocLabel(PdfHeaderMetadata metadata)
        {
            var code  = metadata.DocumentCode?.Trim() ?? string.Empty;
            var title = metadata.DocumentTitle?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(code))   return title;
            if (string.IsNullOrEmpty(title))  return code;
            return $"{code} - {title}";
        }

        private static string Trim(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        // ── Column widths ─────────────────────────────────────────────────────
        private static void ApplyColumnWidths(WorksheetPart worksheetPart)
        {
            var ws   = worksheetPart.Worksheet;
            var cols = ws.GetFirstChild<Columns>();
            if (cols != null)
                cols.Remove();

            cols = new Columns();

            // Insert BEFORE SheetData
            var sheetData = ws.GetFirstChild<SheetData>();
            if (sheetData != null)
                ws.InsertBefore(cols, sheetData);
            else
                ws.Append(cols);

            static Column MakeCol(uint idx, double width) => new Column
            {
                Min           = idx,
                Max           = idx,
                Width         = width,
                CustomWidth   = true,
                BestFit       = false
            };

            cols.Append(MakeCol(1, ColA));
            cols.Append(MakeCol(2, ColB));
            cols.Append(MakeCol(3, ColC));
            cols.Append(MakeCol(4, ColD));
        }

        // ── Freeze pane (lock header rows) ───────────────────────────────────
        private static void ApplyFreezePane(WorksheetPart worksheetPart)
        {
            var ws         = worksheetPart.Worksheet;
            var sheetViews = ws.GetFirstChild<SheetViews>();
            if (sheetViews == null)
            {
                sheetViews = new SheetViews();
                ws.InsertBefore(sheetViews, ws.GetFirstChild<SheetData>());
            }

            var sheetView = sheetViews.GetFirstChild<SheetView>()
                            ?? sheetViews.AppendChild(new SheetView { WorkbookViewId = 0U });

            // Remove any existing pane / selection
            sheetView.RemoveAllChildren<Pane>();
            sheetView.RemoveAllChildren<Selection>();

            sheetView.Append(new Pane
            {
                VerticalSplit   = HeaderRowCount,
                TopLeftCell     = $"A{HeaderRowCount + 1}",
                ActivePane      = PaneValues.BottomLeft,
                State           = PaneStateValues.Frozen
            });

            sheetView.Append(new Selection
            {
                Pane            = PaneValues.BottomLeft,
                ActiveCell      = $"A{HeaderRowCount + 1}",
                SequenceOfReferences = new ListValue<StringValue> { InnerText = $"A{HeaderRowCount + 1}" }
            });
        }

        // ── Row / cell builders ───────────────────────────────────────────────
        private static Row CreateRow(uint index, double height, params (string Column, string Value, uint StyleIndex)[] cells)
        {
            var row = new Row
            {
                RowIndex    = index,
                Height      = height,
                CustomHeight = true
            };

            foreach (var (column, value, styleIndex) in cells)
                row.Append(CreateTextCell(column, index, value, styleIndex));

            return row;
        }

        private static Cell CreateTextCell(string column, uint rowIndex, string value, uint styleIndex) =>
            new Cell
            {
                CellReference = $"{column}{rowIndex}",
                DataType      = CellValues.InlineString,
                StyleIndex    = styleIndex,
                InlineString  = new InlineString(new Text(value ?? string.Empty))
            };

        // ── Remove existing header ────────────────────────────────────────────
        private static void RemoveExistingHeader(SheetData sheetData)
        {
            var firstText = sheetData.Elements<Row>()
                .FirstOrDefault(r => r.RowIndex?.Value == 1)
                ?.Elements<Cell>()
                .FirstOrDefault(c => string.Equals(GetColumnName(c.CellReference?.Value), "A", StringComparison.OrdinalIgnoreCase))
                ?.InnerText;

            if (!IsKnownMarker(firstText))
                return;

            // Detect how many header rows to remove (support different versions)
            uint countToRemove = firstText == LegacyMarker1 || firstText == LegacyMarker2 ? 7u : HeaderRowCount;

            var headerRows = sheetData.Elements<Row>()
                .Where(r => r.RowIndex?.Value <= countToRemove)
                .ToList();

            foreach (var row in headerRows)
                row.Remove();

            ShiftRows(sheetData, -(int)countToRemove);
        }

        private static bool IsKnownMarker(string? text) =>
            string.Equals(text, HeaderMarker,   StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, LegacyMarker1,  StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, LegacyMarker2,  StringComparison.OrdinalIgnoreCase);

        // ── Row shift ─────────────────────────────────────────────────────────
        private static void ShiftRows(SheetData sheetData, int offset)
        {
            if (offset == 0) return;

            foreach (var row in sheetData.Elements<Row>().OrderByDescending(r => r.RowIndex?.Value ?? 0))
            {
                var cur = (int)(row.RowIndex?.Value ?? 0);
                if (cur <= 0) continue;

                var next = Math.Max(1, cur + offset);
                row.RowIndex = (uint)next;

                foreach (var cell in row.Elements<Cell>())
                    UpdateCellReference(cell, (uint)next);
            }
        }

        // ── Styles ────────────────────────────────────────────────────────────
        private static ExcelHeaderStyles EnsureHeaderStyles(WorkbookPart workbookPart)
        {
            var stylesPart = workbookPart.WorkbookStylesPart ?? workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet ??= CreateDefaultStylesheet();

            var ss          = stylesPart.Stylesheet;
            var fonts       = ss.Fonts       ?? ss.AppendChild(new Fonts       { Count = 0U });
            var fills       = ss.Fills       ?? ss.AppendChild(new Fills       { Count = 0U });
            var borders     = ss.Borders     ?? ss.AppendChild(new Borders     { Count = 0U });
            var cellFormats = ss.CellFormats ?? ss.AppendChild(new CellFormats { Count = 0U });

            // Fonts
            var titleLabelFont = AppendFont(fonts, bold: true,  color: "FFFFFF", size: 11d);
            var titleValueFont = AppendFont(fonts, bold: true,  color: "FFFFFF", size: 11d);
            var labelFont      = AppendFont(fonts, bold: true,  color: "1D4D35", size: 9.5d);
            var valueFont      = AppendFont(fonts, bold: false, color: "111827", size: 9.5d);

            // Fills
            var darkGreen   = AppendFill(fills, "0D6B44");   // title dark green
            var midGreen    = AppendFill(fills, "108A57");   // title value slightly lighter
            var lightGreen  = AppendFill(fills, "E6F4ED");   // label cell
            var whiteFill   = AppendFill(fills, "FFFFFF");   // value cell
            var spacerFill  = AppendFill(fills, "F0F9F5");   // spacer row

            // Border
            var thinBorder     = AppendBorder(borders, "B0C4BC");
            var titleBorder    = AppendBorder(borders, "0A5235");

            // Formats
            var titleLabelFmt = AppendCellFormat(cellFormats, titleLabelFont, darkGreen,  titleBorder, HorizontalAlignmentValues.Left,   wrap: false);
            var titleValueFmt = AppendCellFormat(cellFormats, titleValueFont, midGreen,   titleBorder, HorizontalAlignmentValues.Center,  wrap: false);
            var labelFmt      = AppendCellFormat(cellFormats, labelFont,      lightGreen, thinBorder,  HorizontalAlignmentValues.Left);
            var valueFmt      = AppendCellFormat(cellFormats, valueFont,      whiteFill,  thinBorder,  HorizontalAlignmentValues.Left);
            var spacerFmt     = AppendCellFormat(cellFormats, valueFont,      spacerFill, thinBorder,  HorizontalAlignmentValues.Left,   wrap: false);

            stylesPart.Stylesheet.Save();
            return new ExcelHeaderStyles(titleLabelFmt, titleValueFmt, labelFmt, valueFmt, spacerFmt);
        }

        private static Stylesheet CreateDefaultStylesheet() =>
            new Stylesheet(
                new Fonts(new Font())   { Count = 1U },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
                { Count = 2U },
                new Borders(new Border()) { Count = 1U },
                new CellStyleFormats(new CellFormat()) { Count = 1U },
                new CellFormats(new CellFormat())      { Count = 1U },
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U }) { Count = 1U },
                new DifferentialFormats { Count = 0U },
                new TableStyles { Count = 0U, DefaultTableStyle = "TableStyleMedium2", DefaultPivotStyle = "PivotStyleLight16" });

        private static uint AppendFont(Fonts fonts, bool bold, string color, double size)
        {
            var font = new Font(
                new FontSize  { Val = size },
                new Color     { Rgb = new HexBinaryValue(color) },
                new FontName  { Val = "Calibri" });

            if (bold) font.InsertAt(new Bold(), 0);
            fonts.Append(font);
            fonts.Count = (uint)fonts.ChildElements.Count;
            return fonts.Count.Value - 1;
        }

        private static uint AppendFill(Fills fills, string hex)
        {
            fills.Append(new Fill(
                new PatternFill(
                    new ForegroundColor { Rgb = new HexBinaryValue(hex) },
                    new BackgroundColor { Indexed = 64U })
                { PatternType = PatternValues.Solid }));
            fills.Count = (uint)fills.ChildElements.Count;
            return fills.Count.Value - 1;
        }

        private static uint AppendBorder(Borders borders, string hexColor = "D1D5DB")
        {
            borders.Append(new Border(
                new LeftBorder  (MakeBorderColor(hexColor)) { Style = BorderStyleValues.Thin },
                new RightBorder (MakeBorderColor(hexColor)) { Style = BorderStyleValues.Thin },
                new TopBorder   (MakeBorderColor(hexColor)) { Style = BorderStyleValues.Thin },
                new BottomBorder(MakeBorderColor(hexColor)) { Style = BorderStyleValues.Thin },
                new DiagonalBorder()));
            borders.Count = (uint)borders.ChildElements.Count;
            return borders.Count.Value - 1;
        }

        private static Color MakeBorderColor(string hex) => new() { Rgb = new HexBinaryValue(hex) };

        private static uint AppendCellFormat(
            CellFormats cellFormats,
            uint fontId, uint fillId, uint borderId,
            HorizontalAlignmentValues? horizontal = null,
            bool wrap = true)
        {
            cellFormats.Append(new CellFormat
            {
                FontId        = fontId,
                FillId        = fillId,
                BorderId      = borderId,
                ApplyFont     = true,
                ApplyFill     = true,
                ApplyBorder   = true,
                ApplyAlignment = true,
                Alignment     = new Alignment
                {
                    Horizontal = horizontal ?? HorizontalAlignmentValues.Left,
                    Vertical   = VerticalAlignmentValues.Center,
                    WrapText   = wrap
                }
            });
            cellFormats.Count = (uint)cellFormats.ChildElements.Count;
            return cellFormats.Count.Value - 1;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void UpdateCellReference(Cell cell, uint rowIndex)
        {
            var reference = cell.CellReference?.Value;
            if (string.IsNullOrWhiteSpace(reference)) return;
            var column = GetColumnName(reference);
            if (!string.IsNullOrWhiteSpace(column))
                cell.CellReference = $"{column}{rowIndex}";
        }

        private static string GetColumnName(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference)) return string.Empty;
            return new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        }

        private sealed record ExcelHeaderStyles(
            uint TitleLabel,
            uint TitleValue,
            uint Label,
            uint Value,
            uint Spacer);
    }
}
