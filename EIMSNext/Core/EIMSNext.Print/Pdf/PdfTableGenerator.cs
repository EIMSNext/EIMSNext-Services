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
        private PdfSheetRenderPlan? _renderPlan;
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

            _renderPlan = _layoutCalculator.CreatePlan(worksheet, pageSetup, rowIndex => IsRepeatRow(worksheet, rowIndex));
            if (_renderPlan.VisibleRows.Count == 0 || _renderPlan.VisibleColumns.Count == 0) return;

            foreach (var columnIndex in _renderPlan.VisibleColumns)
            {
                var columnWidth = _renderPlan.ColumnWidthsCm.TryGetValue(columnIndex, out var width)
                    ? width
                    : _options.DefaultColumnWidthCm;
                _table.AddColumn(Unit.FromCentimeter(columnWidth));
            }

            foreach (var rowIndex in _renderPlan.VisibleRows)
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

        private bool IsRepeatRow(UniverWorksheet sheet, int rowIdx)
        {
            if (sheet.CellData?.TryGetValue(rowIdx.ToString(), out var row) != true) return false;
            return row!.Values.Any(cell => cell?.PrintMeta?.IsTable() == true);
        }

        private void ProcessRepeatRow(int rowIndex)
        {
            if (_worksheet == null || _worksheet.CellData?.TryGetValue(rowIndex.ToString(), out var row) != true || _renderPlan == null) return;
            if (row == null) return;

            var templateCells = new Dictionary<int, UniverCell>();
            foreach (var (colKey, cell) in row)
            {
                if (int.TryParse(colKey, out int colIndex) && cell != null && _renderPlan.IsVisibleColumn(colIndex))
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
                var rowHeight = _renderPlan.RowHeightsCm.TryGetValue(rowIndex, out var height)
                    ? height
                    : _options.DefaultRowHeightCm;
                pdfRow.Height = Unit.FromCentimeter(rowHeight);

                for (int visibleColumnIndex = 0; visibleColumnIndex < _renderPlan.VisibleColumns.Count; visibleColumnIndex++)
                {
                    var sourceColumnIndex = _renderPlan.VisibleColumns[visibleColumnIndex];
                    var cell = pdfRow.Cells[visibleColumnIndex];

                    if (TryApplyMerge(cell, rowIndex, sourceColumnIndex, repeatMergeOnly: true))
                    {
                        continue;
                    }

                    if (templateCells.TryGetValue(sourceColumnIndex, out var templateCell))
                    {
                        _styleResolver.ApplyCellStyle(cell, templateCell);
                        var cellValue = subDataArray != null && subDataArray.Count > 0
                            ? GetCellValue(templateCell, _data!, new[] { i })
                            : string.Empty;
                        AddCellParagraph(cell, templateCell, cellValue, keepEmptyParagraph: true);
                    }
                }
            }
        }

        private Row ProcessNormalRow(int rowIndex)
        {
            if (_renderPlan == null)
            {
                throw new InvalidOperationException("Render plan is required before rendering rows.");
            }

            var pdfRow = _table!.AddRow();
            var rowHeight = _renderPlan.RowHeightsCm.TryGetValue(rowIndex, out var height)
                ? height
                : _options.DefaultRowHeightCm;
            pdfRow.Height = Unit.FromCentimeter(rowHeight);

            for (int visibleColumnIndex = 0; visibleColumnIndex < _renderPlan.VisibleColumns.Count; visibleColumnIndex++)
            {
                var sourceColumnIndex = _renderPlan.VisibleColumns[visibleColumnIndex];
                var cell = pdfRow.Cells[visibleColumnIndex];

                if (TryApplyMerge(cell, rowIndex, sourceColumnIndex, repeatMergeOnly: false))
                {
                    continue;
                }

                var univerCell = GetCell(_worksheet!, rowIndex, sourceColumnIndex);
                if (univerCell != null)
                {
                    _styleResolver.ApplyCellStyle(cell, univerCell);
                    var cellValue = GetCellValue(univerCell, _data!, Array.Empty<int>());
                    AddCellParagraph(cell, univerCell, cellValue, keepEmptyParagraph: false);
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
            if (_renderPlan?.MergeCells.TryGetValue((rowIndex, columnIndex), out var mergePlan) != true || mergePlan == null)
            {
                return false;
            }

            if (repeatMergeOnly && mergePlan.MergeDown > 0)
            {
                return false;
            }

            if (mergePlan.IsCoveredCell)
            {
                return true;
            }

            cell.MergeRight = mergePlan.MergeRight;
            cell.MergeDown = mergePlan.MergeDown;
            return false;
        }

        private string GetCellValue(UniverCell cell, JsonObject data, int[] indexes)
        {
            if (_isPreview || data == null) return cell.Value?.ToString() ?? string.Empty;

            if (cell.PrintMeta == null) return cell.Value?.ToString() ?? string.Empty;

            return data.GetJsonValue(cell.PrintMeta.GetValuePath(indexes));
        }

        private void AddCellParagraph(Cell cell, UniverCell univerCell, string cellValue, bool keepEmptyParagraph)
        {
            if (string.IsNullOrEmpty(cellValue) && !keepEmptyParagraph)
            {
                return;
            }

            var paragraph = cell.AddParagraph(cellValue ?? string.Empty);
            _styleResolver.ApplyParagraphPadding(paragraph, univerCell);
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
    }
}
