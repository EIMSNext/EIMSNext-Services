using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    public static class PdfConvertUtil
    {
        #region 单位转换（像素→厘米/点，跨平台一致）
        /// <summary>
        /// 像素转MigraDoc厘米（标准DPI：1cm=28.35像素）
        /// </summary>
        /// <param name="pixel">像素值（可空，空值返回1.0cm）</param>
        /// <returns>转换后的厘米值</returns>
        public static double PixelToCm(double? pixel, double defaultValueCm = PdfRenderDefaults.DefaultColumnWidthCm)
        {
            return pixel <= 0 || pixel is null ? defaultValueCm : Math.Round(pixel.Value / 28.35, 4);
        }

        /// <summary>
        /// 像素转MigraDoc边框宽度（点pt，1pt=1.333像素）
        /// </summary>
        /// <param name="pixel">像素值（可空，空值返回0.25pt）</param>
        /// <returns>转换后的点值</returns>
        public static double PixelToPt(double? pixel, double defaultValuePt = PdfRenderDefaults.DefaultBorderWidthPt)
        {
            return pixel <= 0 || pixel is null ? defaultValuePt : Math.Round(pixel.Value / 1.333, 2);
        }
        #endregion

        #region 颜色转换
        /// <summary>
        /// 十六进制颜色转MigraDoc原生Color（支持#RGB/#RRGGBB，空值返回黑色）
        /// 匹配前端UniverJS的颜色存储格式
        /// </summary>
        /// <param name="hexColor">十六进制颜色（可空，如#000/#000000）</param>
        /// <returns>MigraDoc原生Color</returns>
        public static Color HexToMigraColor(string? hexColor, Color? fallbackColor = null)
        {
            var defaultColor = fallbackColor ?? PdfRenderDefaults.DefaultFontColor;

            // 空值/非十六进制，返回默认黑色
            if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#") || (hexColor.Length != 4 && hexColor.Length != 7))
                return defaultColor;

            try
            {
                byte r = 0, g = 0, b = 0;
                // 处理短格式 #RGB
                if (hexColor.Length == 4)
                {
                    r = Convert.ToByte($"{hexColor[1]}{hexColor[1]}", 16);
                    g = Convert.ToByte($"{hexColor[2]}{hexColor[2]}", 16);
                    b = Convert.ToByte($"{hexColor[3]}{hexColor[3]}", 16);
                }
                // 处理标准格式 #RRGGBB
                else if (hexColor.Length == 7)
                {
                    r = Convert.ToByte(hexColor.Substring(1, 2), 16);
                    g = Convert.ToByte(hexColor.Substring(3, 2), 16);
                    b = Convert.ToByte(hexColor.Substring(5, 2), 16);
                }

                // 返回MigraDoc原生Color
                return Color.FromRgb(r, g, b);
            }
            catch
            {
                return defaultColor;
            }
        }
        #endregion

        #region 对齐方式转换
        /// <summary>
        /// 水平对齐（left/center/right）→ MigraDoc段落对齐
        /// </summary>
        /// <param name="hAlign">UniverJS水平对齐值（可空）</param>
        /// <returns>MigraDoc段落对齐</returns>
        public static ParagraphAlignment HAlignToMigra(UniverHorizontalAlignment? hAlign, ParagraphAlignment defaultAlignment)
        {
            return hAlign switch
            {
                UniverHorizontalAlignment.Center => ParagraphAlignment.Center,
                UniverHorizontalAlignment.Right => ParagraphAlignment.Right,
                UniverHorizontalAlignment.Left => ParagraphAlignment.Left,
                _ => defaultAlignment
            };
        }

        public static ParagraphAlignment HAlignToMigra(string? hAlign, ParagraphAlignment defaultAlignment)
        {
            if (string.IsNullOrWhiteSpace(hAlign)) return defaultAlignment;

            return hAlign.Trim().ToLowerInvariant() switch
            {
                "2" => ParagraphAlignment.Center,
                "3" => ParagraphAlignment.Right,
                "center" => ParagraphAlignment.Center,
                "right" => ParagraphAlignment.Right,
                "left" => ParagraphAlignment.Left,
                _ => defaultAlignment
            };
        }

        /// <summary>
        /// 垂直对齐（top/middle/bottom）→ MigraDoc单元格垂直对齐
        /// </summary>
        /// <param name="vAlign">UniverJS垂直对齐值（可空）</param>
        /// <returns>MigraDoc单元格垂直对齐</returns>
        public static VerticalAlignment VAlignToMigra(UniverVerticalAlignment? vAlign, VerticalAlignment defaultAlignment)
        {
            return vAlign switch
            {
                UniverVerticalAlignment.Top => VerticalAlignment.Top,
                UniverVerticalAlignment.Bottom => VerticalAlignment.Bottom,
                UniverVerticalAlignment.Middle => VerticalAlignment.Center,
                _ => defaultAlignment
            };
        }

        public static VerticalAlignment VAlignToMigra(string? vAlign, VerticalAlignment defaultAlignment)
        {
            if (string.IsNullOrWhiteSpace(vAlign)) return defaultAlignment;

            return vAlign.Trim().ToLowerInvariant() switch
            {
                "1" => VerticalAlignment.Top,
                "3" => VerticalAlignment.Bottom,
                "top" => VerticalAlignment.Top,
                "bottom" => VerticalAlignment.Bottom,
                "middle" => VerticalAlignment.Center,
                "center" => VerticalAlignment.Center,
                _ => defaultAlignment
            };
        }
        #endregion

        #region 边框辅助设置（批量设置全边框，避免重复代码）
        /// <summary>
        /// 为单元格设置全边框（上/下/左/右）
        /// </summary>
        /// <param name="borders">单元格边框对象</param>
        /// <param name="color">边框颜色</param>
        /// <param name="width">边框宽度（pt）</param>
        public static void SetCellBorder(Borders borders, Color color, double width)
        {
            if (borders is null) return;

            borders.Top.Color = color;
            borders.Top.Width = width;
            borders.Bottom.Color = color;
            borders.Bottom.Width = width;
            borders.Left.Color = color;
            borders.Left.Width = width;
            borders.Right.Color = color;
            borders.Right.Width = width;
        }
        #endregion
    }
}
