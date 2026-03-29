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

        public PdfTableGenerator(UniverWorkbook workbook, bool isPreview = false)
        {
            _workbook = workbook;
            _isPreview = isPreview;
        }

        public void Generate(UniverWorksheet worksheet, Table table, JsonObject data)
        {
            // 单元格内边距，优化文字与边框间距
            table.TopPadding = 2.0;
            table.BottomPadding = 2.0;
            table.LeftPadding = 2.0;
            table.RightPadding = 2.0;
            // 全局边框置0，逐单元格解析自定义边框
            table.Borders.Width = 0;

            var (maxRow, maxCol) = CalculateEffectiveRange(worksheet);
            if (maxRow == 0 || maxCol == 0) return;

            var mergeMap = BuildMergeMap(worksheet);

            // 设置列宽
            for (int i = 0; i < maxCol; i++)
            {
                var columnWidth = GetColumnWidth(worksheet, i);
                table.AddColumn(Unit.FromCentimeter(columnWidth));
            }

            // 处理每一行
            for (int r = 0; r < maxRow; r++)
            {
                // 检查是否为重复行（包含>符号的字段）
                if (IsRepeatRow(worksheet, r))
                {
                    ProcessRepeatRow(table, worksheet, r, data);
                }
                else
                {
                    ProcessNormalRow(table, worksheet, r, data);
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
            return row.Values.Any(cell =>
                cell?.PrintMeta?.Id?.Contains('>') == true);
        }

        private double GetColumnWidth(UniverWorksheet sheet, int colIndex)
        {
            // 优先使用ColumnWidth字典
            if (sheet.ColumnWidth?.TryGetValue(colIndex, out var width) == true)
                return PdfConvertUtil.PixelToCm(width / 10.0); // 转换为厘米
            
            // 其次使用ColumnData中的宽度
            if (sheet.ColumnData?.TryGetValue(colIndex.ToString(), out var colData) == true && colData.Width.HasValue)
                return PdfConvertUtil.PixelToCm(colData.Width.Value / 10.0); // 转换为厘米
            
            return PdfConvertUtil.PixelToCm(sheet.DefaultColumnWidth / 10.0); // 默认列宽
        }

        private double GetRowHeight(UniverWorksheet sheet, int rowIndex)
        {
            // 优先使用RowHeight字典
            if (sheet.RowHeight?.TryGetValue(rowIndex, out var height) == true)
                return PdfConvertUtil.PixelToCm(height / 10.0); // 转换为厘米
            
            // 其次使用RowData中的高度
            if (sheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true && rowData.Height.HasValue)
                return PdfConvertUtil.PixelToCm(rowData.Height.Value / 10.0); // 转换为厘米
            
            return PdfConvertUtil.PixelToCm(sheet.DefaultRowHeight / 10.0); // 默认行高
        }

        private void ProcessRepeatRow(Table table, UniverWorksheet sheet, int rowIndex, JsonObject data)
        {
            if (sheet.CellData?.TryGetValue(rowIndex.ToString(), out var row) != true) return;

            // 获取重复行的模板单元格
            var templateCells = new Dictionary<int, UniverCell>();
            foreach (var (colKey, cell) in row)
            {
                if (int.TryParse(colKey, out int colIndex) && cell != null)
                {
                    templateCells[colIndex] = cell;
                }
            }

            // 获取数据源键（去掉>后面的部分）
            string dataSourceKey = "";
            foreach (var cell in templateCells.Values)
            {
                if (cell.PrintMeta?.Id?.Contains('>') == true)
                {
                    dataSourceKey = cell.PrintMeta.Id.Split('>')[0];
                    break;
                }
            }

            if (string.IsNullOrEmpty(dataSourceKey)) return;

            // 获取子表数据
            var subDataArray = data.GetJsonArray($"$.{dataSourceKey}") as JsonArray;
            if (subDataArray == null || subDataArray.Count == 0) return;

            // 对子表中的每条数据生成一行
            for (int i = 0; i < subDataArray.Count; i++)
            {
                var pdfRow = table.AddRow();
                var rowHeight = GetRowHeight(sheet, rowIndex);
                pdfRow.Height = Unit.FromCentimeter(rowHeight);

                // 处理每一列
                for (int colIndex = 0; colIndex < GetMaxColumnCount(sheet); colIndex++)
                {
                    var cell = pdfRow.Cells[colIndex];

                    if (templateCells.TryGetValue(colIndex, out var templateCell))
                    {
                        // 设置单元格样式
                        ApplyCellStyle(cell, templateCell);

                        // 设置单元格值（从数据中取值）
                        var cellValue = GetCellValue(templateCell, data, new[] { i });
                        cell.AddParagraph(cellValue);
                    }
                }
            }
        }

        private void ProcessNormalRow(Table table, UniverWorksheet sheet, int rowIndex, JsonObject data)
        {
            var pdfRow = table.AddRow();
            var rowHeight = GetRowHeight(sheet, rowIndex);
            pdfRow.Height = Unit.FromCentimeter(rowHeight);

            var maxCol = GetMaxColumnCount(sheet);

            for (int colIndex = 0; colIndex < maxCol; colIndex++)
            {
                var cell = pdfRow.Cells[colIndex];

                // 获取单元格数据
                var univerCell = GetCell(sheet, rowIndex, colIndex);
                if (univerCell != null)
                {
                    // 设置单元格样式
                    ApplyCellStyle(cell, univerCell);

                    // 设置单元格值
                    var cellValue = GetCellValue(univerCell, data, Array.Empty<int>());
                    cell.AddParagraph(cellValue);
                }
            }
        }

        private int GetMaxColumnCount(UniverWorksheet sheet)
        {
            int maxCol = 0;

            if (sheet.CellData != null)
            {
                foreach (var row in sheet.CellData.Values)
                {
                    foreach (var colKey in row.Keys)
                    {
                        if (int.TryParse(colKey, out int colIndex))
                            maxCol = Math.Max(maxCol, colIndex + 1);
                    }
                }
            }

            if (sheet.MergeData != null)
            {
                foreach (var merge in sheet.MergeData)
                {
                    maxCol = Math.Max(maxCol, merge.EndColumn + 1);
                }
            }

            return maxCol;
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
            // 设置基本样式
            //cell.TopPadding = 2.0;
            //cell.BottomPadding = 2.0;
            //cell.LeftPadding = 2.0;
            //cell.RightPadding = 2.0;

            // 应用样式
            if (!string.IsNullOrEmpty(univerCell.Style) &&
                _workbook.Styles?.Styles?.TryGetValue(univerCell.Style, out var style) == true)
            {
                var fmt = cell.Format;

                // 字体颜色
                if (style.Color?.Rgb != null)
                    fmt.Font.Color = PdfConvertUtil.HexToMigraColor(style.Color.Rgb);

                // 背景色
                if (style.Background?.Rgb != null)
                    cell.Shading.Color = PdfConvertUtil.HexToMigraColor(style.Background.Rgb);

                // 字体样式
                if (style.Bold == 1) fmt.Font.Bold = true;
                if (style.Italic == 1) fmt.Font.Italic = true;
                if (style.FontSize.HasValue) fmt.Font.Size = style.FontSize.Value;
                
                // 字体名称 - 使用改进的字体解析
                if (!string.IsNullOrEmpty(style.FontFamily))
                {
                    fmt.Font.Name = style.FontFamily;
                }

                // 对齐方式
                if (style.HorizontalAlign.HasValue)
                    fmt.Alignment = PdfConvertUtil.HAlignToMigra(style.HorizontalAlign.ToString());

                // 边框
                if (style.Border != null)
                {
                    ApplyBorderStyle(cell, style.Border);
                }
            }
        }

        private void ApplyBorderStyle(Cell cell, UniverBorder border)
        {
            cell.Borders.Visible = true;

            // 上边框
            if (border.Top != null)
                SetBorderSide(cell.Borders.Top, border.Top);

            // 下边框
            if (border.Bottom != null)
                SetBorderSide(cell.Borders.Bottom, border.Bottom);

            // 左边框
            if (border.Left != null)
                SetBorderSide(cell.Borders.Left, border.Left);

            // 右边框
            if (border.Right != null)
                SetBorderSide(cell.Borders.Right, border.Right);
        }

        private void SetBorderSide(Border border, UniverBorderSide side)
        {
            if (side.Style == "none")
            {
                border.Visible = false;
                return;
            }

            border.Visible = true;
            border.Style = side.Style switch
            {
                "dashed" => BorderStyle.DashSmallGap,
                "dotted" => BorderStyle.Dot,
                "dashDot" => BorderStyle.DashDot,
                "dashDotDot" => BorderStyle.DashDotDot,
                "double" => BorderStyle.Single, // PDF 兼容方案：用加粗单线替代
                _ => BorderStyle.Single
            };

            border.Width = side.Style switch
            {
                "thin" => Unit.FromPoint(0.5),
                "medium" or "double" => Unit.FromPoint(1.5), // double 映射为加粗单线
                "thick" => Unit.FromPoint(2.0),
                "dashed" or "dotted" => Unit.FromPoint(0.75),
                _ => Unit.FromPoint(0.75)
            };

            border.Color = PdfConvertUtil.HexToMigraColor(side.Color.Rgb);
        }

        private string GetCellValue(UniverCell cell, JsonObject data, int[] indexes)
        {
            if (_isPreview || data == null) return cell.Value?.ToString() ?? string.Empty;

            if (cell.PrintMeta == null) return cell.Value?.ToString() ?? string.Empty;

            return data.GetJsonValue(cell.PrintMeta.GetJsonPath(indexes));
        }
    }
}
