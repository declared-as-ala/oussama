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
        private const string HeaderMarker = "En-tete QualiFlow";
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

                foreach (var row in BuildHeaderRows(metadata).Reverse())
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

            if (!string.Equals(firstCellText, HeaderMarker, StringComparison.OrdinalIgnoreCase))
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

        private static Row[] BuildHeaderRows(PdfHeaderMetadata metadata)
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
                CreateRow(1, ("A", HeaderMarker)),
                CreateRow(2, ("A", "Organisation"), ("B", org)),
                CreateRow(3, ("A", "Document"), ("B", document)),
                CreateRow(4, ("A", "Version"), ("B", version), ("C", "Statut"), ("D", status)),
                CreateRow(5, ("A", "Processus"), ("B", process), ("C", "Procedure"), ("D", procedure)),
                CreateRow(6, ("A", "Genere le"), ("B", generated)),
                CreateRow(7, ("A", string.Empty))
            };
        }

        private static Row CreateRow(uint index, params (string Column, string Value)[] cells)
        {
            var row = new Row { RowIndex = index };
            foreach (var (column, value) in cells)
            {
                row.Append(CreateTextCell(column, index, value));
            }

            return row;
        }

        private static Cell CreateTextCell(string column, uint rowIndex, string value)
        {
            return new Cell
            {
                CellReference = $"{column}{rowIndex}",
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value ?? string.Empty))
            };
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
    }
}
