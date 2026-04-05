using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    public static class PdfRenderDefaults
    {
        public const string DefaultFontFamily = "FangSong";
        public const double DefaultFontSize = 10;
        public const double DefaultCellPaddingCm = 0.1;
        public const double DefaultBorderWidthPt = 0.75;
        public const double DefaultColumnWidthCm = 1.9;
        public const double DefaultRowHeightCm = 0.69;
        public const bool DefaultWrapText = false;
        public const bool IgnoreUnsupportedStyle = true;
        public static readonly string TemporaryDirectory = Path.Combine(Path.GetTempPath(), "eimsnext-print", "temp");

        public static readonly Color DefaultFontColor = Color.FromRgb(0, 0, 0);
        public static readonly Color DefaultBorderColor = Color.FromRgb(0, 0, 0);
        public static readonly Color? DefaultBackgroundColor = null;

        public static readonly ParagraphAlignment DefaultHorizontalAlignment = ParagraphAlignment.Left;
        public static readonly VerticalAlignment DefaultVerticalAlignment = VerticalAlignment.Center;
        public static readonly Underline DefaultUnderline = Underline.None;

        public const PageFormat DefaultPageFormat = PageFormat.A4;
        public const Orientation DefaultOrientation = Orientation.Portrait;
        public static readonly Unit DefaultPageTopMargin = Unit.FromCentimeter(1.0);
        public static readonly Unit DefaultPageBottomMargin = Unit.FromCentimeter(1.0);
        public static readonly Unit DefaultPageLeftMargin = Unit.FromCentimeter(1.0);
        public static readonly Unit DefaultPageRightMargin = Unit.FromCentimeter(1.0);

        public static readonly IReadOnlyDictionary<string, string> FontAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["simsun"] = "SimSun",
                ["宋体"] = "SimSun",
                ["simsun-extb"] = "SimSun",
                ["nsimsun"] = "NSimSun",
                ["新宋体"] = "NSimSun",
                ["simfang"] = "FangSong",
                ["仿宋"] = "FangSong",
                ["fangsong"] = "FangSong",
                ["microsoftyahei"] = "MicrosoftYaHeiUI",
                ["微软雅黑"] = "MicrosoftYaHeiUI",
                ["microsoftyaheiui"] = "MicrosoftYaHeiUI",
                ["微软雅黑ui"] = "MicrosoftYaHeiUI",
                ["timesnewroman"] = "Times New Roman",
                ["times"] = "Times New Roman",
                ["arial"] = "Arial",
                ["verdana"] = "Verdana",
                ["tahoma"] = "Tahoma"
            };

        public static readonly IReadOnlyList<string> FontFallbackChain =
        [
            "FangSong",
            "MicrosoftYaHeiUI",
            "Arial",
            "Times New Roman"
        ];
    }
}
