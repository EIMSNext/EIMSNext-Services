using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    internal sealed class PdfStyleResolver
    {
        private readonly UniverWorkbook _workbook;
        private readonly PdfRenderOptions _options;

        public PdfStyleResolver(UniverWorkbook workbook, PdfRenderOptions options)
        {
            _workbook = workbook;
            _options = options;
        }

        public void ApplyCellStyle(Cell cell, UniverCell? univerCell)
        {
            if (univerCell == null || string.IsNullOrEmpty(univerCell.Style))
            {
                return;
            }

            if (_workbook.Styles?.TryGetValue(univerCell.Style, out var style) != true || style == null)
            {
                return;
            }

            var format = cell.Format;
            var fontFamily = PdfFontResolverRuntime.ResolveFontFamily(style.FontFamily, style.Bold == 1, style.Italic == 1);
            format.Font.Name = fontFamily;
            format.Font.Size = style.FontSize ?? _options.DefaultFontSize;
            format.Font.Bold = style.Bold == 1;
            format.Font.Italic = style.Italic == 1;
            format.Font.Color = PdfConvertUtil.HexToMigraColor(style.Color?.Rgb, _options.DefaultFontColor);

            if (style.Background?.Rgb != null)
            {
                cell.Shading.Color = PdfConvertUtil.HexToMigraColor(style.Background.Rgb, _options.DefaultFontColor);
            }

            if (style.HorizontalAlign.HasValue)
            {
                format.Alignment = PdfConvertUtil.HAlignToMigra(style.HorizontalAlign.ToString(), ParagraphAlignment.Left);
            }

            if (style.VerticalAlign.HasValue)
            {
                cell.VerticalAlignment = PdfConvertUtil.VAlignToMigra(style.VerticalAlign.ToString(), VerticalAlignment.Center);
            }
            else
            {
                cell.VerticalAlignment = VerticalAlignment.Center;
            }

            if (style.Underline?.Style.GetValueOrDefault() > 0)
            {
                format.Font.Underline = Underline.Single;
                if (style.Underline.Color?.Rgb != null)
                {
                    format.Font.Color = PdfConvertUtil.HexToMigraColor(style.Underline.Color.Rgb, format.Font.Color);
                }
            }

            ApplyBorders(cell, style.Border);
        }

        public void ApplyMergedOuterBorders(Cell cell, UniverCell? rightEdgeCell, UniverCell? bottomEdgeCell, UniverCell? bottomRightEdgeCell)
        {
            cell.Borders.Visible = true;
            ApplyMergedBorderSide(cell.Borders.Right, rightEdgeCell, style => style.Border?.Right);
            ApplyMergedBorderSide(cell.Borders.Bottom, bottomEdgeCell, style => style.Border?.Bottom);

            if ((rightEdgeCell == null || GetStyle(rightEdgeCell)?.Border?.Right == null) && bottomRightEdgeCell != null)
            {
                ApplyMergedBorderSide(cell.Borders.Right, bottomRightEdgeCell, style => style.Border?.Right);
            }

            if ((bottomEdgeCell == null || GetStyle(bottomEdgeCell)?.Border?.Bottom == null) && bottomRightEdgeCell != null)
            {
                ApplyMergedBorderSide(cell.Borders.Bottom, bottomRightEdgeCell, style => style.Border?.Bottom);
            }
        }

        public void ApplyParagraphPadding(Paragraph paragraph, UniverCell? univerCell)
        {
            paragraph.Format.LeftIndent = Unit.FromCentimeter(0.06);
            paragraph.Format.RightIndent = Unit.FromCentimeter(0.06);
            paragraph.Format.SpaceBefore = Unit.FromCentimeter(0.01);
            paragraph.Format.SpaceAfter = Unit.FromCentimeter(0.01);
        }

        private void ApplyBorders(Cell cell, UniverBorder? border)
        {
            if (border == null)
            {
                return;
            }

            cell.Borders.Visible = true;

            ApplyBorderSide(cell.Borders.Top, border.Top);
            ApplyBorderSide(cell.Borders.Bottom, border.Bottom);
            ApplyBorderSide(cell.Borders.Left, border.Left);
            ApplyBorderSide(cell.Borders.Right, border.Right);
        }

        private void ApplyBorderSide(Border border, UniverBorderSide? side)
        {
            if (side == null)
            {
                return;
            }

            if (string.Equals(side.StyleName, "none", StringComparison.OrdinalIgnoreCase))
            {
                border.Visible = false;
                return;
            }

            border.Visible = true;
            border.Style = side.StyleName switch
            {
                "dashed" => BorderStyle.DashSmallGap,
                "dotted" => BorderStyle.Dot,
                "dashdot" => BorderStyle.DashDot,
                "dashdotdot" => BorderStyle.DashDotDot,
                "double" => BorderStyle.Single,
                _ => BorderStyle.Single
            };
            border.Width = Unit.FromPoint(side.WidthValue > 0 ? side.WidthValue : _options.DefaultBorderWidthPt);
            border.Color = PdfConvertUtil.HexToMigraColor(side.Color?.Rgb, _options.DefaultBorderColor);
        }

        private void ApplyMergedBorderSide(Border border, UniverCell? univerCell, Func<UniverStyle, UniverBorderSide?> selector)
        {
            var style = GetStyle(univerCell);
            var side = style == null ? null : selector(style);
            if (side == null)
            {
                return;
            }

            ApplyBorderSide(border, side);
            border.Visible = true;
        }

        private UniverStyle? GetStyle(UniverCell? univerCell)
        {
            if (univerCell == null || string.IsNullOrEmpty(univerCell.Style))
            {
                return null;
            }

            return _workbook.Styles?.TryGetValue(univerCell.Style, out var style) == true ? style : null;
        }
    }
}
