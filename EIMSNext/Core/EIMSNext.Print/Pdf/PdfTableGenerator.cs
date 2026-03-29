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
        private UniverWorksheet _worksheet;
        private Table _table;
        private JsonObject _data;
        private int _maxCol;

        public PdfTableGenerator(UniverWorkbook workbook, bool isPreview = false)
        {
            _workbook = workbook;
            _isPreview = isPreview;
        }

        public void Generate(UniverWorksheet worksheet, Table table, JsonObject data)
        {
            // 设置类级变量
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
            
            // 全局边框置0，逐单元格解析自定义边框
            table.Borders.Width = 0;

            var (maxRow, maxCol) = CalculateEffectiveRange(worksheet);
            if (maxRow == 0 || maxCol == 0) return;
            
            _maxCol = maxCol;

            var mergeMap = BuildMergeMap(worksheet);

            // 设置列宽
            double totalWidth = 0;
            for (int i = 0; i < maxCol; i++)
            {
                var columnWidth = GetColumnWidth(_worksheet, i);
                totalWidth += columnWidth;
            }
            
            // 计算页面可用宽度（A4纸宽度21cm，减去左右边距）
            double availableWidth = 21.0 - 1 - 1; // 左右边距为0
            
            // 如果总宽度超过可用宽度，按比例缩放列宽
            double scaleFactor = totalWidth > availableWidth ? availableWidth / totalWidth : 1.0;
            
            // 添加列并设置宽度
            for (int i = 0; i < maxCol; i++)
            {
                var columnWidth = GetColumnWidth(_worksheet, i) * scaleFactor;
                _table.AddColumn(Unit.FromCentimeter(columnWidth));
            }

            // 处理每一行
            for (int r = 0; r < maxRow; r++)
            {
                // 检查是否为重复行（包含>符号的字段）
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
            return row.Values.Any(cell =>
                cell?.PrintMeta?.Id?.Contains('>') == true);
        }

        private double GetColumnWidth(UniverWorksheet sheet, int colIndex)
        {
            // 优先使用ColumnWidth字典
            if (sheet.ColumnWidth?.TryGetValue(colIndex, out var width) == true)
                return PdfConvertUtil.PixelToCm(width); // 转换为厘米
            
            // 其次使用ColumnData中的宽度
            if (sheet.ColumnData?.TryGetValue(colIndex.ToString(), out var colData) == true && colData.Width.HasValue)
                return PdfConvertUtil.PixelToCm(colData.Width.Value); // 转换为厘米
            
            return PdfConvertUtil.PixelToCm(sheet.DefaultColumnWidth); // 默认列宽
        }

        private double GetRowHeight(UniverWorksheet sheet, int rowIndex)
        {
            // 优先使用RowHeight字典
            if (sheet.RowHeight?.TryGetValue(rowIndex, out var height) == true)
                return PdfConvertUtil.PixelToCm(height); // 转换为厘米
            
            // 其次使用RowData中的高度
            if (sheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true && rowData.Height.HasValue)
                return PdfConvertUtil.PixelToCm(rowData.Height.Value); // 转换为厘米
            
            return PdfConvertUtil.PixelToCm(sheet.DefaultRowHeight); // 默认行高
        }

        private void ProcessRepeatRow(int rowIndex)
        {
            if (_worksheet.CellData?.TryGetValue(rowIndex.ToString(), out var row) != true) return;

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
            var subDataArray = _data.GetJsonArray($"$.{dataSourceKey}") as JsonArray;
            if (subDataArray == null || subDataArray.Count == 0) return;

            // 对子表中的每条数据生成一行
            for (int i = 0; i < subDataArray.Count; i++)
            {
                var pdfRow = _table.AddRow();
                var rowHeight = GetRowHeight(_worksheet, rowIndex);
                pdfRow.Height = Unit.FromCentimeter(rowHeight);

                // 处理每一列
                for (int colIndex = 0; colIndex < _maxCol; colIndex++)
                {
                    var cell = pdfRow.Cells[colIndex];

                    if (templateCells.TryGetValue(colIndex, out var templateCell))
                    {
                        // 设置单元格样式
                        ApplyCellStyle(cell, templateCell);

                        // 设置单元格值（从数据中取值）
                        var cellValue = GetCellValue(templateCell, _data, new[] { i });
                        cell.AddParagraph(cellValue);
                    }
                }
            }
        }

        private void ProcessNormalRow(int rowIndex)
        {
            var pdfRow = _table.AddRow();
            var rowHeight = GetRowHeight(_worksheet, rowIndex);
            pdfRow.Height = Unit.FromCentimeter(rowHeight);

            for (int colIndex = 0; colIndex < _maxCol; colIndex++)
            {
                var cell = pdfRow.Cells[colIndex];

                // 获取单元格数据
                var univerCell = GetCell(_worksheet, rowIndex, colIndex);
                if (univerCell != null)
                {
                    // 设置单元格样式
                    ApplyCellStyle(cell, univerCell);

                    // 设置单元格值
                    var cellValue = GetCellValue(univerCell, _data, Array.Empty<int>());
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
            // 应用样式
            if (!string.IsNullOrEmpty(univerCell.Style) &&
                _workbook.Styles?.TryGetValue(univerCell.Style, out var style) == true)
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

                // 水平对齐方式
                if (style.HorizontalAlign.HasValue)
                    fmt.Alignment = PdfConvertUtil.HAlignToMigra(style.HorizontalAlign.ToString());
                
                // 垂直对齐方式
                if (style.VerticalAlign.HasValue)
                    cell.VerticalAlignment = PdfConvertUtil.VAlignToMigra(style.VerticalAlign.ToString());
                else
                    // 默认居中
                    cell.VerticalAlignment = VerticalAlignment.Center;
                
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
                "double" => BorderStyle.Single, // PDF 兼容方案：用加粗单线替代
                _ => BorderStyle.Single
            };

            // 恢复边框粗细设置
            border.Width = Unit.FromPoint(side.WidthValue);

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
