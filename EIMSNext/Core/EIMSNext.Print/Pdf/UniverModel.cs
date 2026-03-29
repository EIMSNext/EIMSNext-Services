using System.Text.Json.Serialization;

namespace EIMSNext.Print.Pdf
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UniverRangeType { Normal, Column, Row, All }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UniverHorizontalAlignment { Left, Center, Right }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UniverVerticalAlignment { Top, Middle, Bottom }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UniverTextWrap { Overflow, Wrap, Clip }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UniverTextDirection { LeftToRight, RightToLeft, Context }

    public class UniverColor
    {
        public string Rgb { get; set; } = "#000000";
    }

    public class UniverRange
    {
        public int StartRow { get; set; }
        public int StartColumn { get; set; }
        public int EndRow { get; set; }
        public int EndColumn { get; set; }
        public UniverRangeType? RangeType { get; set; }
        public string? SheetId { get; set; }
        public string? Name { get; set; }

        [JsonIgnore]
        public bool IsSingleCell => StartRow == EndRow && StartColumn == EndColumn;

        [JsonIgnore]
        public int Area => (EndRow - StartRow + 1) * (EndColumn - StartColumn + 1);
    }

    public class UniverMergeInfo
    {
        public int StartRow { get; set; }
        public int StartColumn { get; set; }
        public int EndRow { get; set; }
        public int EndColumn { get; set; }

        public bool IsMasterCell(int row, int col) => row == StartRow && col == StartColumn;
    }

    public class UniverCustomFormat
    {
        public string Format { get; set; } = "General";
        [JsonPropertyName("tb")] public int? TextBold { get; set; }
        [JsonPropertyName("it")] public int? TextItalic { get; set; }
        [JsonPropertyName("cl")] public UniverColor? TextColor { get; set; }
    }

    public class UniverHyperlink
    {
        public string Url { get; set; } = string.Empty;
        public string? Tooltip { get; set; }
        public string? Location { get; set; }
        public string? Display { get; set; }
    }

    public class UniverTextRotation
    {
        public double Angle { get; set; } = 0;
        public bool Vertical { get; set; } = false;
    }

    public class UniverCell
    {
        [JsonPropertyName("v")] public object? Value { get; set; }
        [JsonPropertyName("s")] public string? Style { get; set; }
        [JsonPropertyName("ct")] public UniverCustomFormat? CustomFormat { get; set; }
        [JsonPropertyName("mc")] public UniverMergeInfo? MergeInfo { get; set; }
        [JsonPropertyName("l")] public UniverHyperlink? Hyperlink { get; set; }
        [JsonPropertyName("tr")] public UniverTextRotation? TextRotation { get; set; }
        [JsonPropertyName("custom")] public PdfPrintMeta? PrintMeta { get; set; }
    }

    public class UniverWorksheet
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, Dictionary<string, UniverCell>>? CellData { get; set; } = new();
        public List<UniverRange>? MergeData { get; set; } = new();
        public double DefaultRowHeight { get; set; } = 19.5;
        public double DefaultColumnWidth { get; set; } = 54;
        public Dictionary<int, double>? RowHeight { get; set; } = new();
        public Dictionary<int, double>? ColumnWidth { get; set; } = new();
        public List<UniverImageData>? Images { get; set; } = new();
        public Dictionary<string, UniverRowData>? RowData { get; set; } = new(); // 行数据信息
        public Dictionary<string, UniverColumnData>? ColumnData { get; set; } = new(); // 列数据信息        
      }

    public class UniverStyle
    {
        [JsonPropertyName("bg")] public UniverColor? Background { get; set; }
        [JsonPropertyName("bl")] public int? Bold { get; set; }
        [JsonPropertyName("it")] public int? Italic { get; set; }
        [JsonPropertyName("ff")] public string? FontFamily { get; set; } = FallbackFontResolver.DefaultFontName;
        [JsonPropertyName("fs")] public double? FontSize { get; set; } = 10;
        [JsonPropertyName("cl")] public UniverColor? Color { get; set; }
        [JsonPropertyName("ht")] public UniverHorizontalAlignment? HorizontalAlign { get; set; }
        [JsonPropertyName("vt")] public UniverVerticalAlignment? VerticalAlign { get; set; }
        [JsonPropertyName("tb")] public UniverTextWrap? TextWrap { get; set; }
        [JsonPropertyName("bd")] public UniverBorder? Border { get; set; }
        [JsonPropertyName("pd")] public UniverPadding? Padding { get; set; }
        [JsonPropertyName("tp")] public UniverTextDirection? TextDirection { get; set; }
        [JsonPropertyName("ul")] public UniverUnderline? Underline { get; set; } // 下划线
    }

    public class UniverUnderline
    {
        [JsonPropertyName("s")] public int? Style { get; set; } = 0; // 下划线样式
        [JsonPropertyName("cl")] public UniverColor? Color { get; set; } // 下划线颜色
    }

    public class UniverBorder
    {
        [JsonPropertyName("t")] public UniverBorderSide? Top { get; set; }
        [JsonPropertyName("b")] public UniverBorderSide? Bottom { get; set; }
        [JsonPropertyName("l")] public UniverBorderSide? Left { get; set; }
        [JsonPropertyName("r")] public UniverBorderSide? Right { get; set; }
    }

    public class UniverBorderSide
    {
        [JsonPropertyName("s")]
        public object Style { get; set; } = "none"; // 支持数字和字符串两种格式
        
        [JsonPropertyName("cl")]
        public UniverColor Color { get; set; } = new();
        
        [JsonIgnore]
        public string StyleName
        {
            get
            {
                return Style switch
                {
                    string s => s,
                    int i => i switch
                    {
                        1 => "single",
                        2 => "dashed",
                        3 => "dotted",
                        4 => "double",
                        _ => "single"
                    },
                    _ => "single"
                };
            }
        }
        
        [JsonIgnore]
        public double WidthValue
        {
            get
            {
                // 根据样式类型返回对应的边框宽度
                return StyleName switch
                {
                    "thin" => 0.5,
                    "medium" => 1.0,
                    "thick" => 2.0,
                    "double" => 1.5,
                    _ => 0.75 // 默认宽度
                };
            }
        }
    }

    public class UniverPadding
    {
        public double Top { get; set; } = 2;
        public double Bottom { get; set; } = 2;
        public double Left { get; set; } = 2;
        public double Right { get; set; } = 2;
    }

    public class UniverImageData
    {
        public string ImageId { get; set; } = string.Empty;
        public UniverImagePosition Position { get; set; } = new();
        public string ResourceId { get; set; } = string.Empty;
    }

    public class UniverImagePosition
    {
        public double Top { get; set; }
        public double Left { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class UniverResource
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }

    public class UniverRowData
    {
        [JsonPropertyName("h")] public double? Height { get; set; }
        [JsonPropertyName("hd")] public int? Hidden { get; set; }
        [JsonPropertyName("ia")] public int? AutoHeight { get; set; }
        [JsonPropertyName("ah")] public double? ActualHeight { get; set; }
    }

    public class UniverColumnData
    {
        [JsonPropertyName("w")] public double? Width { get; set; }
        [JsonPropertyName("hd")] public int? Hidden { get; set; }
        [JsonPropertyName("iw")] public double? InnerWidth { get; set; }
    }

    public class UniverWorkbook
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, UniverWorksheet> Sheets { get; set; } = new();
        public object? Snapshot { get; set; }
        public List<UniverResource>? Resources { get; set; }
        public Dictionary<string, UniverStyle>? Styles { get; set; }
        
        [JsonIgnore]
        public UniverWorksheet? ActiveSheet => Sheets?.Values.FirstOrDefault();
    }

}
