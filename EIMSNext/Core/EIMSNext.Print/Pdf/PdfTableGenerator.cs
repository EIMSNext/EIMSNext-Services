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

        // 类级变量
        private UniverWorksheet? _worksheet;
        private Table? _table;
        private JsonObject? _data;
        private int _maxCol;

        public PdfTableGenerator(UniverWorkbook workbook, bool isPreview = false)
        {
            _workbook = workbook;
            _isPreview = isPreview;
        }

        public void Generate(UniverWorksheet worksheet, Table table, JsonObject data)
        {
            _worksheet = worksheet;
            _table = table;
            _data = data;

            table.Format.LeftIndent = Unit.FromCentimeter(0);
            table.Format.RightIndent = Unit.FromCentimeter(0);
            table.Format.FirstLineIndent = Unit.FromCentimeter(0);
            table.Format.Alignment = ParagraphAlignment.Left;
            table.TopPadding = Unit.FromCentimeter(0);
            table.BottomPadding = Unit.FromCentimeter(0);
            table.LeftPadding = Unit.FromCentimeter(0);
            table.RightPadding = Unit.FromCentimeter(0);

            table.Borders.Width = 0;

            var (maxRow, maxCol) = CalculateEffectiveRange(worksheet);
            if (maxRow == 0 || maxCol == 0) return;

            _maxCol = maxCol;

            var mergeMap = BuildMergeMap(worksheet);

            double totalWidth = 0;
            for (int i = 0; i < maxCol; i++)
            {
                var columnWidth = GetColumnWidth(_worksheet, i);
                totalWidth += columnWidth;
            }

            double availableWidth = 21.0 - 1 - 1;
            double scaleFactor = totalWidth > availableWidth ? availableWidth / totalWidth : 1.0;

            for (int i = 0; i < maxCol; i++)
            {
                var columnWidth = GetColumnWidth(_worksheet, i) * scaleFactor;
                _table.AddColumn(Unit.FromCentimeter(columnWidth));
            }

            for (int r = 0; r < maxRow; r++)
            {
                if (IsRepeatRow(_worksheet, r))
                {
                    ProcessRepeatRow(r);
                }
                else
                {
                    ProcessNormalRow(r);
                }
            }
        }

        private (int, int) CalculateEffectiveRange(UniverWorksheet sheet)
        {
            int maxRow = 0, maxCol = 0;

            if (sheet.CellData != null)
            {
                foreach (var (rk, row) in sheet.CellData)
                {
                    if (!int.TryParse(rk, out int r)) continue;
                    maxRow = Math.Max(maxRow, r + 1);
                    foreach (var (ck, _) in row)
                    {
                        if (int.TryParse(ck, out int c))
                            maxCol = Math.Max(maxCol, c + 1);
                    }
                }
            }

            if (sheet.MergeData != null)
            {
                foreach (var m in sheet.MergeData)
                {
                    maxRow = Math.Max(maxRow, m.EndRow + 1);
                    maxCol = Math.Max(maxCol, m.EndColumn + 1);
                }
            }

            return (maxRow, maxCol);
        }

        private (Dictionary<(int, int), (int, int)>, HashSet<(int, int)>) BuildMergeMap(UniverWorksheet sheet)
        {
            var starts = new Dictionary<(int, int), (int, int)>();
            var occupied = new HashSet<(int, int)>();

            if (sheet.MergeData == null) return (starts, occupied);

            foreach (var m in sheet.MergeData)
            {
                var span = (m.EndRow - m.StartRow + 1, m.EndColumn - m.StartColumn + 1);
                starts[(m.StartRow, m.StartColumn)] = span;

                for (int r = m.StartRow; r <= m.EndRow; r++)
                    for (int c = m.StartColumn; c <= m.EndColumn; c++)
                        occupied.Add((r, c));
            }
            return (starts, occupied);
        }

        private bool IsRepeatRow(UniverWorksheet sheet, int rowIdx)
        {
            if (sheet.CellData?.TryGetValue(rowIdx.ToString(), out var row) != true) return false;
            return row!.Values.Any(cell => cell?.PrintMeta?.IsTable() == true);
        }

        private double GetColumnWidth(UniverWorksheet sheet, int colIndex)
        {
            if (sheet.ColumnWidth?.TryGetValue(colIndex, out var width) == true)
                return PdfConvertUtil.PixelToCm(width);

            if (sheet.ColumnData?.TryGetValue(colIndex.ToString(), out var colData) == true && colData.Width.HasValue)
                return PdfConvertUtil.PixelToCm(colData.Width.Value);

            return PdfConvertUtil.PixelToCm(sheet.DefaultColumnWidth);
        }

        private double GetRowHeight(UniverWorksheet sheet, int rowIndex)
        {
            if (sheet.RowHeight?.TryGetValue(rowIndex, out var height) == true)
                return PdfConvertUtil.PixelToCm(height);

            if (sheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true && rowData.Height.HasValue)
                return PdfConvertUtil.PixelToCm(rowData.Height.Value);

            return PdfConvertUtil.PixelToCm(sheet.DefaultRowHeight);
        }

        private void ProcessRepeatRow(int rowIndex)
        {
            if (_worksheet!.CellData?.TryGetValue(rowIndex.ToString(), out var row) != true) return;

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
            if (subDataArray == null || subDataArray.Count == 0) return;

            for (int i = 0; i < subDataArray.Count; i++)
            {
                var pdfRow = _table!.AddRow();
                var rowHeight = GetRowHeight(_worksheet, rowIndex);
                pdfRow.Height = Unit.FromCentimeter(rowHeight);

                for (int colIndex = 0; colIndex < _maxCol; colIndex++)
                {
                    var cell = pdfRow.Cells[colIndex];

                    if (templateCells.TryGetValue(colIndex, out var templateCell))
                    {
                        ApplyCellStyle(cell, templateCell);
                        var cellValue = GetCellValue(templateCell, _data!, new[] { i });
                        var para = cell.AddParagraph(cellValue);
                        para.Format.LeftIndent = Unit.FromCentimeter(0.1);
                    }
                }
            }
        }

        private void ProcessNormalRow(int rowIndex)
        {
            var pdfRow = _table!.AddRow();
            var rowHeight = GetRowHeight(_worksheet!, rowIndex);
            pdfRow.Height = Unit.FromCentimeter(rowHeight);

            for (int colIndex = 0; colIndex < _maxCol; colIndex++)
            {
                var cell = pdfRow.Cells[colIndex];

                var univerCell = GetCell(_worksheet!, rowIndex, colIndex);
                if (univerCell != null)
                {
                    ApplyCellStyle(cell, univerCell);
                    var cellValue = GetCellValue(univerCell, _data!, Array.Empty<int>());
                    cell.AddParagraph(cellValue);
                }
            }
        }


        private UniverCell? GetCell(UniverWorksheet sheet, int rowIndex, int colIndex)
        {
            if (sheet.CellData?.TryGetValue(rowIndex.ToString(), out var row) == true)
            {
                return row.GetValueOrDefault(colIndex.ToString());
            }
            return null;
        }

        private void ApplyCellStyle(Cell cell, UniverCell univerCell)
        {
            if (!string.IsNullOrEmpty(univerCell.Style) &&
                _workbook.Styles?.TryGetValue(univerCell.Style, out var style) == true)
            {
                var fmt = cell.Format;

                if (style.Color?.Rgb != null)
                    fmt.Font.Color = PdfConvertUtil.HexToMigraColor(style.Color.Rgb);

                if (style.Background?.Rgb != null)
                    cell.Shading.Color = PdfConvertUtil.HexToMigraColor(style.Background.Rgb);

                if (style.Bold == 1) fmt.Font.Bold = true;
                if (style.Italic == 1) fmt.Font.Italic = true;
                if (style.FontSize.HasValue) fmt.Font.Size = style.FontSize.Value;

                if (!string.IsNullOrEmpty(style.FontFamily))
                {
                    fmt.Font.Name = style.FontFamily;
                }

                if (style.Underline != null && style.Underline.Style.HasValue && style.Underline.Style.Value > 0)
                {
                    fmt.Font.Underline = Underline.Single;
                    if (style.Underline.Color?.Rgb != null)
                    {
                        fmt.Font.Color = PdfConvertUtil.HexToMigraColor(style.Underline.Color.Rgb);
                    }
                }

                if (style.HorizontalAlign.HasValue)
                    fmt.Alignment = PdfConvertUtil.HAlignToMigra(style.HorizontalAlign.ToString());

                if (style.VerticalAlign.HasValue)
                    cell.VerticalAlignment = PdfConvertUtil.VAlignToMigra(style.VerticalAlign.ToString());
                else
                    cell.VerticalAlignment = VerticalAlignment.Center;

                if (style.Border != null)
                {
                    ApplyBorderStyle(cell, style.Border);
                }
            }
        }

        private void ApplyBorderStyle(Cell cell, UniverBorder border)
        {
            cell.Borders.Visible = true;

            if (border.Top != null)
                SetBorderSide(cell.Borders.Top, border.Top);

            if (border.Bottom != null)
                SetBorderSide(cell.Borders.Bottom, border.Bottom);

            if (border.Left != null)
                SetBorderSide(cell.Borders.Left, border.Left);

            if (border.Right != null)
                SetBorderSide(cell.Borders.Right, border.Right);
        }

        private void SetBorderSide(Border border, UniverBorderSide side)
        {
            if (side.StyleName == "none")
            {
                border.Visible = false;
                return;
            }

            border.Visible = true;
            border.Style = side.StyleName switch
            {
                "dashed" => BorderStyle.DashSmallGap,
                "dotted" => BorderStyle.Dot,
                "dashDot" => BorderStyle.DashDot,
                "dashDotDot" => BorderStyle.DashDotDot,
                "double" => BorderStyle.Single,
                _ => BorderStyle.Single
            };

            border.Width = Unit.FromPoint(side.WidthValue);
            border.Color = PdfConvertUtil.HexToMigraColor(side.Color.Rgb);
        }

        private string GetCellValue(UniverCell cell, JsonObject data, int[] indexes)
        {
            if (_isPreview || data == null) return cell.Value?.ToString() ?? string.Empty;

            if (cell.PrintMeta == null) return cell.Value?.ToString() ?? string.Empty;

            return data.GetJsonValue(cell.PrintMeta.GetValuePath(indexes));
        }
    }
}
