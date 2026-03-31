using System.Text.Json.Nodes;
using JianJieYun.Print.Common.Extension;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    public class PdfTableGenerator
    {
        private readonly UniverWorkbook _workbook;
        private readonly bool _isPreview;
        private readonly PdfRenderOptions _options;
        private readonly PdfStyleResolver _styleResolver;
        private readonly PdfSheetLayoutCalculator _layoutCalculator;

        // 类级变量
        private UniverWorksheet? _worksheet;
        private Table? _table;
        private JsonObject? _data;
        private int _maxCol;
        private Dictionary<(int Row, int Column), (int RowSpan, int ColumnSpan)> _mergeStarts = new();
        private HashSet<(int Row, int Column)> _mergeOccupied = new();
        private Row? _lastGeneratedNormalRow;
        private bool _lastRenderedRowWasRepeat;

        public PdfTableGenerator(UniverWorkbook workbook, PdfRenderOptions options, bool isPreview = false)
        {
            _workbook = workbook;
            _options = options;
            _isPreview = isPreview;
            _styleResolver = new PdfStyleResolver(workbook, options);
            _layoutCalculator = new PdfSheetLayoutCalculator(options);
        }

        public void Generate(UniverWorksheet worksheet, Table table, JsonObject data, PageSetup pageSetup)
        {
            _worksheet = worksheet;
            _table = table;
            _data = data;
            _mergeStarts = new Dictionary<(int Row, int Column), (int RowSpan, int ColumnSpan)>();
            _mergeOccupied = new HashSet<(int Row, int Column)>();
            _lastGeneratedNormalRow = null;
            _lastRenderedRowWasRepeat = false;

            table.Format.LeftIndent = Unit.FromCentimeter(0);
            table.Format.RightIndent = Unit.FromCentimeter(0);
            table.Format.FirstLineIndent = Unit.FromCentimeter(0);
            table.Format.Alignment = _options.DefaultHorizontalAlignment;
            table.TopPadding = Unit.FromCentimeter(0);
            table.BottomPadding = Unit.FromCentimeter(0);
            table.LeftPadding = Unit.FromCentimeter(0);
            table.RightPadding = Unit.FromCentimeter(0);

            table.Borders.Width = 0;

            var (maxRow, maxCol) = CalculateEffectiveRange(worksheet);
            if (maxRow == 0 || maxCol == 0) return;

            _maxCol = maxCol;
            (_mergeStarts, _mergeOccupied) = BuildMergeMap(worksheet);

            double totalWidth = 0;
            for (int columnIndex = 0; columnIndex < maxCol; columnIndex++)
            {
                totalWidth += GetColumnWidth(_worksheet, columnIndex);
            }

            var availableWidth = GetAvailableWidthCm(pageSetup);
            var scaleFactor = totalWidth > availableWidth ? availableWidth / totalWidth : 1.0;

            for (int columnIndex = 0; columnIndex < maxCol; columnIndex++)
            {
                var columnWidth = GetColumnWidth(_worksheet, columnIndex) * scaleFactor;
                _table.AddColumn(Unit.FromCentimeter(columnWidth));
            }

            for (int rowIndex = 0; rowIndex < maxRow; rowIndex++)
            {
                if (IsRepeatRow(_worksheet, rowIndex))
                {
                    MarkDetailHeaderBoundary();
                    ProcessRepeatRow(rowIndex);
                    _lastRenderedRowWasRepeat = true;
                }
                else
                {
                    _lastGeneratedNormalRow = ProcessNormalRow(rowIndex);
                    _lastRenderedRowWasRepeat = false;
                }
            }
        }

        private (int MaxRow, int MaxCol) CalculateEffectiveRange(UniverWorksheet sheet)
        {
            int maxRow = 0;
            int maxCol = 0;

            if (sheet.CellData != null)
            {
                foreach (var (rowKey, row) in sheet.CellData)
                {
                    if (!int.TryParse(rowKey, out int rowIndex))
                    {
                        continue;
                    }

                    maxRow = Math.Max(maxRow, rowIndex + 1);
                    foreach (var (columnKey, _) in row)
                    {
                        if (int.TryParse(columnKey, out int columnIndex))
                        {
                            maxCol = Math.Max(maxCol, columnIndex + 1);
                        }
                    }
                }
            }

            if (sheet.MergeData != null)
            {
                foreach (var merge in sheet.MergeData)
                {
                    maxRow = Math.Max(maxRow, merge.EndRow + 1);
                    maxCol = Math.Max(maxCol, merge.EndColumn + 1);
                }
            }

            return (maxRow, maxCol);
        }

        private (Dictionary<(int Row, int Column), (int RowSpan, int ColumnSpan)> Starts, HashSet<(int Row, int Column)> Occupied) BuildMergeMap(UniverWorksheet sheet)
        {
            var starts = new Dictionary<(int Row, int Column), (int RowSpan, int ColumnSpan)>();
            var occupied = new HashSet<(int Row, int Column)>();

            if (sheet.MergeData == null)
            {
                return (starts, occupied);
            }

            foreach (var merge in sheet.MergeData)
            {
                var rowSpan = merge.EndRow - merge.StartRow + 1;
                var columnSpan = merge.EndColumn - merge.StartColumn + 1;
                starts[(merge.StartRow, merge.StartColumn)] = (rowSpan, columnSpan);

                for (int rowIndex = merge.StartRow; rowIndex <= merge.EndRow; rowIndex++)
                {
                    for (int columnIndex = merge.StartColumn; columnIndex <= merge.EndColumn; columnIndex++)
                    {
                        occupied.Add((rowIndex, columnIndex));
                    }
                }
            }

            return (starts, occupied);
        }

        private double GetColumnWidth(UniverWorksheet sheet, int columnIndex)
        {
            if (sheet.ColumnWidth?.TryGetValue(columnIndex, out var width) == true)
            {
                return PdfConvertUtil.PixelToCm(width, _options.DefaultColumnWidthCm);
            }

            if (sheet.ColumnData?.TryGetValue(columnIndex.ToString(), out var columnData) == true && columnData.Width.HasValue)
            {
                return PdfConvertUtil.PixelToCm(columnData.Width.Value, _options.DefaultColumnWidthCm);
            }

            return PdfConvertUtil.PixelToCm(sheet.DefaultColumnWidth, _options.DefaultColumnWidthCm);
        }

        private double GetRowHeight(UniverWorksheet sheet, int rowIndex)
        {
            if (sheet.RowHeight?.TryGetValue(rowIndex, out var height) == true)
            {
                return PdfConvertUtil.PixelToCm(height, _options.DefaultRowHeightCm);
            }

            if (sheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true && rowData.Height.HasValue)
            {
                return PdfConvertUtil.PixelToCm(rowData.Height.Value, _options.DefaultRowHeightCm);
            }

            return PdfConvertUtil.PixelToCm(sheet.DefaultRowHeight, _options.DefaultRowHeightCm);
        }

        private static double GetAvailableWidthCm(PageSetup pageSetup)
        {
            var available = pageSetup.PageWidth - pageSetup.LeftMargin - pageSetup.RightMargin;
            return Math.Max(available.Centimeter, 1);
        }

        private bool IsRepeatRow(UniverWorksheet sheet, int rowIdx)
        {
            if (sheet.CellData?.TryGetValue(rowIdx.ToString(), out var row) != true) return false;
            return row!.Values.Any(cell => cell?.PrintMeta?.IsTable() == true);
        }

        private void ProcessRepeatRow(int rowIndex)
        {
            if (_worksheet == null || _worksheet.CellData?.TryGetValue(rowIndex.ToString(), out var row) != true) return;
            if (row == null) return;

            var templateCells = new Dictionary<int, UniverCell>();
            foreach (var (colKey, cell) in row)
            {
                if (int.TryParse(colKey, out int colIndex) && cell != null)
                {
                    templateCells[colIndex] = cell;
                }
            }

            string tablePath = "";
            foreach (var cell in templateCells.Values)
            {
                if (cell.PrintMeta?.IsTable() == true)
                {
                    tablePath = cell.PrintMeta.GetTablePath();
                    break;
                }
            }

            if (string.IsNullOrEmpty(tablePath)) return;

            var subDataArray = _data!.GetJsonArray(tablePath) as JsonArray;
            var repeatCount = subDataArray?.Count > 0 ? subDataArray.Count : 1;

            for (int i = 0; i < repeatCount; i++)
            {
                var pdfRow = _table!.AddRow();
                var rowHeight = GetRowHeight(_worksheet, rowIndex);
                pdfRow.Height = Unit.FromCentimeter(rowHeight);
                pdfRow.HeightRule = GetRowHeightRule(rowIndex);

                for (int columnIndex = 0; columnIndex < _maxCol; columnIndex++)
                {
                    var cell = pdfRow.Cells[columnIndex];
                    var templateCell = templateCells.GetValueOrDefault(columnIndex);
                    var isCoveredMergeCell = IsCoveredMergeCell(rowIndex, columnIndex, repeatMergeOnly: true, out var ownerSpan);

                    if (isCoveredMergeCell)
                    {
                        if (templateCell != null && ownerSpan.RowSpan == 1 && ownerSpan.ColumnSpan > 1)
                        {
                            _styleResolver.ApplyCellStyle(cell, templateCell);
                        }

                        continue;
                    }

                    if (TryApplyMerge(cell, rowIndex, columnIndex, repeatMergeOnly: true))
                    {
                        continue;
                    }

                    if (templateCell != null)
                    {
                        _styleResolver.ApplyCellStyle(cell, templateCell);
                        ApplyMergedBoundaryStyles(cell, rowIndex, columnIndex);
                        var cellValue = subDataArray != null && subDataArray.Count > 0
                            ? GetCellValue(templateCell, _data!, new[] { i })
                            : string.Empty;
                        AddCellParagraph(cell, templateCell, cellValue, keepEmptyParagraph: true, leftIndentCm: 0.1, rowIndex: rowIndex, columnIndex: columnIndex);
                    }
                }
            }
        }

        private Row ProcessNormalRow(int rowIndex)
        {
            var pdfRow = _table!.AddRow();
            var rowHeight = GetRowHeight(_worksheet!, rowIndex);
            pdfRow.Height = Unit.FromCentimeter(rowHeight);
            pdfRow.HeightRule = GetRowHeightRule(rowIndex);

            for (int columnIndex = 0; columnIndex < _maxCol; columnIndex++)
            {
                var cell = pdfRow.Cells[columnIndex];
                var univerCell = GetCell(_worksheet!, rowIndex, columnIndex);
                var isCoveredMergeCell = IsCoveredMergeCell(rowIndex, columnIndex, repeatMergeOnly: false, out var ownerSpan);

                if (isCoveredMergeCell)
                {
                    if (univerCell != null && ownerSpan.RowSpan == 1 && ownerSpan.ColumnSpan > 1)
                    {
                        _styleResolver.ApplyCellStyle(cell, univerCell);
                    }

                        continue;
                }

                if (TryApplyMerge(cell, rowIndex, columnIndex, repeatMergeOnly: false))
                {
                    continue;
                }

                if (univerCell != null)
                {
                    _styleResolver.ApplyCellStyle(cell, univerCell);
                    ApplyMergedBoundaryStyles(cell, rowIndex, columnIndex);
                    var cellValue = GetCellValue(univerCell, _data!, Array.Empty<int>());
                    AddCellParagraph(cell, univerCell, cellValue, keepEmptyParagraph: false, leftIndentCm: 0, rowIndex: rowIndex, columnIndex: columnIndex);
                }
            }

            return pdfRow;
        }


        private UniverCell? GetCell(UniverWorksheet sheet, int rowIndex, int colIndex)
        {
            if (sheet.CellData?.TryGetValue(rowIndex.ToString(), out var row) == true)
            {
                return row.GetValueOrDefault(colIndex.ToString());
            }
            return null;
        }

        private bool TryApplyMerge(Cell cell, int rowIndex, int columnIndex, bool repeatMergeOnly)
        {
            if (!_mergeOccupied.Contains((rowIndex, columnIndex)))
            {
                return false;
            }

            if (!_mergeStarts.TryGetValue((rowIndex, columnIndex), out var span))
            {
                return true;
            }

            if (repeatMergeOnly && span.RowSpan > 1)
            {
                return false;
            }

            cell.MergeRight = span.ColumnSpan - 1;
            cell.MergeDown = span.RowSpan - 1;
            return false;
        }

        private bool IsCoveredMergeCell(int rowIndex, int columnIndex, bool repeatMergeOnly, out (int RowSpan, int ColumnSpan) ownerSpan)
        {
            if (!_mergeOccupied.Contains((rowIndex, columnIndex)))
            {
                ownerSpan = default;
                return false;
            }

            if (_mergeStarts.ContainsKey((rowIndex, columnIndex)))
            {
                ownerSpan = default;
                return false;
            }

            if (!TryGetMergeOwner(rowIndex, columnIndex, out var ownerRow, out var ownerColumn, out var span))
            {
                ownerSpan = default;
                return false;
            }

            if (repeatMergeOnly && span.RowSpan > 1)
            {
                ownerSpan = span;
                return false;
            }

            ownerSpan = span;
            return true;
        }

        private string GetCellValue(UniverCell cell, JsonObject data, int[] indexes)
        {
            if (_isPreview || data == null) return cell.Value?.ToString() ?? string.Empty;

            if (cell.PrintMeta == null) return cell.Value?.ToString() ?? string.Empty;

            return data.GetJsonValue(cell.PrintMeta.GetValuePath(indexes));
        }

        private void AddCellParagraph(Cell cell, UniverCell univerCell, string cellValue, bool keepEmptyParagraph, double leftIndentCm, int rowIndex, int columnIndex)
        {
            if (string.IsNullOrEmpty(cellValue) && !keepEmptyParagraph)
            {
                return;
            }

            var paragraph = cell.AddParagraph(cellValue ?? string.Empty);
            _styleResolver.ApplyParagraphPadding(paragraph, univerCell);
            paragraph.Format.LeftIndent = Unit.FromCentimeter(Math.Max(leftIndentCm, 0.06));
            paragraph.Format.Alignment = cell.Format.Alignment;
            paragraph.Format.Font.Name = PdfTextFontHelper.ResolveParagraphFontName(cellValue, cell.Format.Font.Name, cell.Format.Font.Bold);
            paragraph.Format.Font.Size = cell.Format.Font.Size;
            paragraph.Format.Font.Bold = cell.Format.Font.Bold;
            paragraph.Format.Font.Italic = cell.Format.Font.Italic;
            paragraph.Format.Font.Color = cell.Format.Font.Color;
            paragraph.Format.Font.Underline = cell.Format.Font.Underline;
        }

        private void MarkDetailHeaderBoundary()
        {
            if (_lastGeneratedNormalRow == null || _lastRenderedRowWasRepeat)
            {
                return;
            }

            _lastGeneratedNormalRow.HeadingFormat = true;
            _lastGeneratedNormalRow.KeepWith = Math.Max(_lastGeneratedNormalRow.KeepWith, 1);
        }

        private void ApplyMergedBoundaryStyles(Cell cell, int rowIndex, int columnIndex)
        {
            if (!_mergeStarts.TryGetValue((rowIndex, columnIndex), out var span))
            {
                return;
            }

            if (span.RowSpan == 1 && span.ColumnSpan == 1)
            {
                return;
            }

            var endRow = rowIndex + span.RowSpan - 1;
            var endColumn = columnIndex + span.ColumnSpan - 1;

            var rightEdgeCell = endColumn != columnIndex ? GetCell(_worksheet!, rowIndex, endColumn) : null;
            var bottomEdgeCell = endRow != rowIndex ? GetCell(_worksheet!, endRow, columnIndex) : null;
            var bottomRightEdgeCell = endRow != rowIndex || endColumn != columnIndex ? GetCell(_worksheet!, endRow, endColumn) : null;

            _styleResolver.ApplyMergedOuterBorders(cell, rightEdgeCell, bottomEdgeCell, bottomRightEdgeCell);
        }

        private RowHeightRule GetRowHeightRule(int rowIndex)
        {
            if (_worksheet?.CellData?.TryGetValue(rowIndex.ToString(), out var row) != true || row == null)
            {
                return RowHeightRule.AtLeast;
            }

            foreach (var cell in row.Values)
            {
                if (cell?.Style == null)
                {
                    continue;
                }

                if (_workbook.Styles?.TryGetValue(cell.Style, out var style) == true && style?.TextWrap == UniverTextWrap.Clip)
                {
                    return RowHeightRule.Exactly;
                }
            }

            return RowHeightRule.AtLeast;
        }

        private bool TryGetMergeOwner(int rowIndex, int columnIndex, out int ownerRow, out int ownerColumn, out (int RowSpan, int ColumnSpan) span)
        {
            foreach (var mergeStart in _mergeStarts)
            {
                var startRow = mergeStart.Key.Row;
                var startColumn = mergeStart.Key.Column;
                var currentSpan = mergeStart.Value;
                var endRow = startRow + currentSpan.RowSpan - 1;
                var endColumn = startColumn + currentSpan.ColumnSpan - 1;

                if (rowIndex >= startRow && rowIndex <= endRow && columnIndex >= startColumn && columnIndex <= endColumn)
                {
                    ownerRow = startRow;
                    ownerColumn = startColumn;
                    span = currentSpan;
                    return true;
                }
            }

            ownerRow = 0;
            ownerColumn = 0;
            span = default;
            return false;
        }

    }
}
