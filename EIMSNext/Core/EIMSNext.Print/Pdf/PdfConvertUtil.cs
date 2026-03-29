using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    public static class PdfConvertUtil
    {
        public const double DefaultCellWidth = 3.0; //cm

        private static readonly Color Black = Color.FromRgb(0, 0, 0);

        #region 单位转换（像素→厘米/点，跨平台一致）
        /// <summary>
        /// 像素转MigraDoc厘米（标准DPI：1cm=28.35像素）
        /// </summary>
        /// <param name="pixel">像素值（可空，空值返回1.0cm）</param>
        /// <returns>转换后的厘米值</returns>
        public static double PixelToCm(double? pixel)
        {
            return pixel <= 0 || pixel is null ? 1.0 : Math.Round(pixel.Value / 28.35, 2);
        }

        /// <summary>
        /// 像素转MigraDoc边框宽度（点pt，1pt=1.333像素）
        /// </summary>
        /// <param name="pixel">像素值（可空，空值返回0.25pt）</param>
        /// <returns>转换后的点值</returns>
        public static double PixelToPt(double? pixel)
        {
            return pixel <= 0 || pixel is null ? 0.25 : Math.Round(pixel.Value / 1.333, 2);
        }
        #endregion

        #region 颜色转换（无System.Drawing，自定义解析十六进制→MigraDoc Color）
        /// <summary>
        /// 十六进制颜色转MigraDoc原生Color（支持#RGB/#RRGGBB，空值返回黑色）
        /// 匹配前端UniverJS的颜色存储格式
        /// </summary>
        /// <param name="hexColor">十六进制颜色（可空，如#000/#000000）</param>
        /// <returns>MigraDoc原生Color</returns>
        public static Color HexToMigraColor(string? hexColor)
        {
            // 空值/非十六进制，返回默认黑色
            if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#") || (hexColor.Length != 4 && hexColor.Length != 7))
                return Black;

            try
            {
                byte r = 0, g = 0, b = 0;
                // 处理短格式 #RGB → #RRGGBB
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

                // 返回MigraDoc原生Color，无任何外部依赖
                return Color.FromRgb(r, g, b);
            }
            catch
            {
                return Black;
            }
        }
        #endregion

        #region 对齐方式转换（UniverJS → MigraDoc，可空判断）
        /// <summary>
        /// 水平对齐（left/center/right）→ MigraDoc段落对齐
        /// </summary>
        /// <param name="hAlign">UniverJS水平对齐值（可空）</param>
        /// <returns>MigraDoc段落对齐</returns>
        public static ParagraphAlignment HAlignToMigra(string? hAlign)
        {
            if (string.IsNullOrEmpty(hAlign)) return ParagraphAlignment.Left;

            return hAlign.ToLower() switch
            {
                "center" => ParagraphAlignment.Center,
                "right" => ParagraphAlignment.Right,
                _ => ParagraphAlignment.Left
            };
        }

        /// <summary>
        /// 垂直对齐（top/middle/bottom）→ MigraDoc单元格垂直对齐
        /// </summary>
        /// <param name="vAlign">UniverJS垂直对齐值（可空）</param>
        /// <returns>MigraDoc单元格垂直对齐</returns>
        public static VerticalAlignment VAlignToMigra(string? vAlign)
        {
            if (string.IsNullOrEmpty(vAlign)) return VerticalAlignment.Center;

            return vAlign.ToLower() switch
            {
                "top" => VerticalAlignment.Top,
                "bottom" => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Center
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
