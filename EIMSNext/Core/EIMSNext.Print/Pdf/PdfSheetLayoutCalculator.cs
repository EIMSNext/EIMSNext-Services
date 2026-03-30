using MigraDoc.DocumentObjectModel;

namespace EIMSNext.Print.Pdf
{
    internal sealed class PdfSheetLayoutCalculator
    {
        private readonly PdfRenderOptions _options;

        public PdfSheetLayoutCalculator(PdfRenderOptions options)
        {
            _options = options;
        }

        public PdfSheetRenderPlan CreatePlan(UniverWorksheet worksheet, PageSetup pageSetup, Func<int, bool>? isRepeatRow = null)
        {
            var (maxRow, maxCol) = CalculateEffectiveRange(worksheet);
            if (maxRow == 0 || maxCol == 0)
            {
                return new PdfSheetRenderPlan();
            }

            var visibleRows = Enumerable.Range(0, maxRow).Where(row => !IsHiddenRow(worksheet, row)).ToList();
            var visibleColumns = Enumerable.Range(0, maxCol).Where(column => !IsHiddenColumn(worksheet, column)).ToList();
            var rowMap = visibleRows.Select((row, index) => (row, index)).ToDictionary(x => x.row, x => x.index);
            var columnMap = visibleColumns.Select((column, index) => (column, index)).ToDictionary(x => x.column, x => x.index);

            var rowHeights = visibleRows.ToDictionary(row => row, row => GetRowHeightCm(worksheet, row));
            var baseColumnWidths = visibleColumns.ToDictionary(column => column, column => GetColumnWidthCm(worksheet, column));

            var availableWidthCm = GetAvailableWidthCm(pageSetup);
            var totalWidthCm = baseColumnWidths.Values.Sum();
            var scaleFactor = totalWidthCm > 0 && totalWidthCm > availableWidthCm ? availableWidthCm / totalWidthCm : 1.0;
            var scaledColumnWidths = baseColumnWidths.ToDictionary(x => x.Key, x => Math.Round(x.Value * scaleFactor, 4));

            var mergePlan = BuildMergePlan(worksheet, rowMap, columnMap, isRepeatRow);

            return new PdfSheetRenderPlan
            {
                VisibleRows = visibleRows,
                VisibleColumns = visibleColumns,
                RowMap = rowMap,
                ColumnMap = columnMap,
                RowHeightsCm = rowHeights,
                ColumnWidthsCm = scaledColumnWidths,
                MergeCells = mergePlan,
                ScaleFactor = scaleFactor
            };
        }

        public double MapHorizontalPixelsToCm(UniverWorksheet worksheet, double pixels, double scaleFactor)
        {
            return MapPixelsToCm(
                pixels,
                index => GetColumnWidthPixels(worksheet, index),
                index => IsHiddenColumn(worksheet, index),
                scaleFactor);
        }

        public double MapVerticalPixelsToCm(UniverWorksheet worksheet, double pixels)
        {
            return MapPixelsToCm(
                pixels,
                index => GetRowHeightPixels(worksheet, index),
                index => IsHiddenRow(worksheet, index),
                1.0);
        }

        private static (int MaxRow, int MaxCol) CalculateEffectiveRange(UniverWorksheet sheet)
        {
            int maxRow = 0;
            int maxCol = 0;

            if (sheet.CellData != null)
            {
                foreach (var (rk, row) in sheet.CellData)
                {
                    if (!int.TryParse(rk, out var rowIndex)) continue;
                    maxRow = Math.Max(maxRow, rowIndex + 1);

                    foreach (var (ck, _) in row)
                    {
                        if (!int.TryParse(ck, out var columnIndex)) continue;
                        maxCol = Math.Max(maxCol, columnIndex + 1);
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

        private double GetColumnWidthCm(UniverWorksheet sheet, int columnIndex)
        {
            if (sheet.ColumnWidth?.TryGetValue(columnIndex, out var width) == true)
            {
                return PdfConvertUtil.PixelToCm(width, _options.DefaultColumnWidthCm);
            }

            if (sheet.ColumnData?.TryGetValue(columnIndex.ToString(), out var columnData) == true)
            {
                if (columnData.InnerWidth.HasValue)
                {
                    return PdfConvertUtil.PixelToCm(columnData.InnerWidth.Value, _options.DefaultColumnWidthCm);
                }

                if (columnData.Width.HasValue)
                {
                    return PdfConvertUtil.PixelToCm(columnData.Width.Value, _options.DefaultColumnWidthCm);
                }
            }

            return PdfConvertUtil.PixelToCm(sheet.DefaultColumnWidth, _options.DefaultColumnWidthCm);
        }

        private double GetColumnWidthPixels(UniverWorksheet sheet, int columnIndex)
        {
            if (sheet.ColumnWidth?.TryGetValue(columnIndex, out var width) == true && width > 0)
            {
                return width;
            }

            if (sheet.ColumnData?.TryGetValue(columnIndex.ToString(), out var columnData) == true)
            {
                if (columnData.InnerWidth.HasValue && columnData.InnerWidth.Value > 0)
                {
                    return columnData.InnerWidth.Value;
                }

                if (columnData.Width.HasValue && columnData.Width.Value > 0)
                {
                    return columnData.Width.Value;
                }
            }

            return sheet.DefaultColumnWidth > 0 ? sheet.DefaultColumnWidth : _options.DefaultColumnWidthCm * 28.35;
        }

        private double GetRowHeightCm(UniverWorksheet sheet, int rowIndex)
        {
            if (sheet.RowHeight?.TryGetValue(rowIndex, out var height) == true)
            {
                return PdfConvertUtil.PixelToCm(height, _options.DefaultRowHeightCm);
            }

            if (sheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true)
            {
                if (rowData.ActualHeight.HasValue)
                {
                    return PdfConvertUtil.PixelToCm(rowData.ActualHeight.Value, _options.DefaultRowHeightCm);
                }

                if (rowData.Height.HasValue)
                {
                    return PdfConvertUtil.PixelToCm(rowData.Height.Value, _options.DefaultRowHeightCm);
                }
            }

            return PdfConvertUtil.PixelToCm(sheet.DefaultRowHeight, _options.DefaultRowHeightCm);
        }

        private double GetRowHeightPixels(UniverWorksheet sheet, int rowIndex)
        {
            if (sheet.RowHeight?.TryGetValue(rowIndex, out var height) == true && height > 0)
            {
                return height;
            }

            if (sheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true)
            {
                if (rowData.ActualHeight.HasValue && rowData.ActualHeight.Value > 0)
                {
                    return rowData.ActualHeight.Value;
                }

                if (rowData.Height.HasValue && rowData.Height.Value > 0)
                {
                    return rowData.Height.Value;
                }
            }

            return sheet.DefaultRowHeight > 0 ? sheet.DefaultRowHeight : _options.DefaultRowHeightCm * 28.35;
        }

        private static double GetAvailableWidthCm(PageSetup pageSetup)
        {
            var available = pageSetup.PageWidth - pageSetup.LeftMargin - pageSetup.RightMargin;
            return Math.Max(available.Centimeter, 1);
        }

        private static bool IsHiddenRow(UniverWorksheet sheet, int rowIndex)
        {
            return sheet.RowData?.TryGetValue(rowIndex.ToString(), out var rowData) == true && rowData.Hidden == 1;
        }

        private static bool IsHiddenColumn(UniverWorksheet sheet, int columnIndex)
        {
            return sheet.ColumnData?.TryGetValue(columnIndex.ToString(), out var columnData) == true && columnData.Hidden == 1;
        }

        private static double MapPixelsToCm(double pixels, Func<int, double> sizeAccessor, Func<int, bool> isHiddenAccessor, double scaleFactor)
        {
            if (pixels <= 0)
            {
                return 0;
            }

            var remaining = pixels;
            var index = 0;
            double mappedCm = 0;

            while (remaining > 0)
            {
                var rawSize = Math.Max(sizeAccessor(index), 1);
                var consumed = Math.Min(remaining, rawSize);
                if (!isHiddenAccessor(index))
                {
                    mappedCm += PdfConvertUtil.PixelToCm(consumed, 0) * scaleFactor;
                }

                remaining -= rawSize;
                index++;
            }

            return Math.Round(mappedCm, 4);
        }

        private static IReadOnlyDictionary<(int Row, int Column), PdfMergeCellPlan> BuildMergePlan(
            UniverWorksheet worksheet,
            IReadOnlyDictionary<int, int> rowMap,
            IReadOnlyDictionary<int, int> columnMap,
            Func<int, bool>? isRepeatRow)
        {
            var result = new Dictionary<(int Row, int Column), PdfMergeCellPlan>();
            if (worksheet.MergeData == null)
            {
                return result;
            }

            foreach (var merge in worksheet.MergeData)
            {
                var mergeTouchesRepeatRow = isRepeatRow != null && Enumerable.Range(merge.StartRow, merge.EndRow - merge.StartRow + 1).Any(isRepeatRow);
                if (mergeTouchesRepeatRow && merge.StartRow != merge.EndRow)
                {
                    continue;
                }

                var visibleRows = Enumerable.Range(merge.StartRow, merge.EndRow - merge.StartRow + 1)
                    .Where(rowMap.ContainsKey)
                    .OrderBy(row => row)
                    .ToList();
                var visibleColumns = Enumerable.Range(merge.StartColumn, merge.EndColumn - merge.StartColumn + 1)
                    .Where(columnMap.ContainsKey)
                    .OrderBy(column => column)
                    .ToList();

                if (visibleRows.Count == 0 || visibleColumns.Count == 0)
                {
                    continue;
                }

                var masterRow = visibleRows[0];
                var masterColumn = visibleColumns[0];

                for (var rowIndex = 0; rowIndex < visibleRows.Count; rowIndex++)
                {
                    for (var columnIndex = 0; columnIndex < visibleColumns.Count; columnIndex++)
                    {
                        var key = (visibleRows[rowIndex], visibleColumns[columnIndex]);
                        result[key] = new PdfMergeCellPlan
                        {
                            IsMasterCell = rowIndex == 0 && columnIndex == 0,
                            IsCoveredCell = !(rowIndex == 0 && columnIndex == 0),
                            MergeRight = rowIndex == 0 && columnIndex == 0 ? visibleColumns.Count - 1 : 0,
                            MergeDown = rowIndex == 0 && columnIndex == 0 ? visibleRows.Count - 1 : 0
                        };
                    }
                }
            }

            return result;
        }
    }
}
