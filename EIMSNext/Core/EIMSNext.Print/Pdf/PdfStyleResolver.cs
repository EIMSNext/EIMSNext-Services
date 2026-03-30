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
            var format = cell.Format;
            ApplyDefaults(cell);

            if (univerCell == null || string.IsNullOrEmpty(univerCell.Style))
            {
                return;
            }

            if (_workbook.Styles?.TryGetValue(univerCell.Style, out var style) != true || style == null)
            {
                return;
            }

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

            format.Alignment = PdfConvertUtil.HAlignToMigra(style.HorizontalAlign, _options.DefaultHorizontalAlignment);
            cell.VerticalAlignment = PdfConvertUtil.VAlignToMigra(style.VerticalAlign, _options.DefaultVerticalAlignment);

            if (style.Underline?.Style.GetValueOrDefault() > 0)
            {
                format.Font.Underline = Underline.Single;
                if (style.Underline.Color?.Rgb != null)
                {
                    format.Font.Color = PdfConvertUtil.HexToMigraColor(style.Underline.Color.Rgb, format.Font.Color);
                }
            }

            ApplyPadding(cell, style.Padding);
            ApplyBorders(cell, style.Border);
            ApplyWrap(cell, style.TextWrap);
        }

        public void ApplyParagraphPadding(Paragraph paragraph, UniverCell? univerCell)
        {
            var padding = ResolvePadding(univerCell);
            paragraph.Format.LeftIndent = Unit.FromPoint(padding.Left);
            paragraph.Format.RightIndent = Unit.FromPoint(padding.Right);
            paragraph.Format.SpaceBefore = Unit.FromPoint(padding.Top);
            paragraph.Format.SpaceAfter = Unit.FromPoint(padding.Bottom);
        }

        private void ApplyDefaults(Cell cell)
        {
            var format = cell.Format;
            format.Font.Name = _options.DefaultFontFamily;
            format.Font.Size = _options.DefaultFontSize;
            format.Font.Color = _options.DefaultFontColor;
            format.Font.Underline = _options.DefaultUnderline;
            format.Alignment = _options.DefaultHorizontalAlignment;
            cell.VerticalAlignment = _options.DefaultVerticalAlignment;

            format.LeftIndent = Unit.FromCentimeter(_options.DefaultCellPaddingCm);
            format.RightIndent = Unit.FromCentimeter(_options.DefaultCellPaddingCm);
            format.SpaceBefore = Unit.FromCentimeter(_options.DefaultCellPaddingCm);
            format.SpaceAfter = Unit.FromCentimeter(_options.DefaultCellPaddingCm);
        }

        private void ApplyPadding(Cell cell, UniverPadding? padding)
        {
            var resolvedPadding = padding ?? ResolvePadding(null);
            cell.Format.LeftIndent = Unit.FromPoint(resolvedPadding.Left);
            cell.Format.RightIndent = Unit.FromPoint(resolvedPadding.Right);
            cell.Format.SpaceBefore = Unit.FromPoint(resolvedPadding.Top);
            cell.Format.SpaceAfter = Unit.FromPoint(resolvedPadding.Bottom);
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

        private static void ApplyWrap(Cell cell, UniverTextWrap? textWrap)
        {
            if (textWrap == UniverTextWrap.Wrap)
            {
                cell.Format.KeepTogether = false;
            }
        }

        private UniverPadding ResolvePadding(UniverCell? univerCell)
        {
            if (univerCell != null &&
                !string.IsNullOrEmpty(univerCell.Style) &&
                _workbook.Styles?.TryGetValue(univerCell.Style, out var style) == true &&
                style?.Padding != null)
            {
                return style.Padding;
            }

            var defaultPaddingPt = Unit.FromCentimeter(_options.DefaultCellPaddingCm).Point;
            return new UniverPadding
            {
                Top = defaultPaddingPt,
                Bottom = defaultPaddingPt,
                Left = defaultPaddingPt,
                Right = defaultPaddingPt
            };
        }
    }
}
