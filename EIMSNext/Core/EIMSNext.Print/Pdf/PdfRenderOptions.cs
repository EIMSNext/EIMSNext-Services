using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Pdf
{
    public class PdfRenderOptions
    {
        public string DefaultFontFamily { get; init; } = PdfRenderDefaults.DefaultFontFamily;
        public double DefaultFontSize { get; init; } = PdfRenderDefaults.DefaultFontSize;
        public Color DefaultFontColor { get; init; } = PdfRenderDefaults.DefaultFontColor;
        public Color DefaultBorderColor { get; init; } = PdfRenderDefaults.DefaultBorderColor;
        public Color? DefaultBackgroundColor { get; init; } = PdfRenderDefaults.DefaultBackgroundColor;
        public ParagraphAlignment DefaultHorizontalAlignment { get; init; } = PdfRenderDefaults.DefaultHorizontalAlignment;
        public VerticalAlignment DefaultVerticalAlignment { get; init; } = PdfRenderDefaults.DefaultVerticalAlignment;
        public Underline DefaultUnderline { get; init; } = PdfRenderDefaults.DefaultUnderline;
        public double DefaultCellPaddingCm { get; init; } = PdfRenderDefaults.DefaultCellPaddingCm;
        public double DefaultBorderWidthPt { get; init; } = PdfRenderDefaults.DefaultBorderWidthPt;
        public double DefaultColumnWidthCm { get; init; } = PdfRenderDefaults.DefaultColumnWidthCm;
        public double DefaultRowHeightCm { get; init; } = PdfRenderDefaults.DefaultRowHeightCm;
        public bool DefaultWrapText { get; init; } = PdfRenderDefaults.DefaultWrapText;
        public bool IgnoreUnsupportedStyle { get; init; } = PdfRenderDefaults.IgnoreUnsupportedStyle;
        public string TemporaryDirectory { get; init; } = PdfRenderDefaults.TemporaryDirectory;

        public PageFormat PageFormat { get; init; } = PdfRenderDefaults.DefaultPageFormat;
        public Orientation Orientation { get; init; } = PdfRenderDefaults.DefaultOrientation;
        public Unit PageTopMargin { get; init; } = PdfRenderDefaults.DefaultPageTopMargin;
        public Unit PageBottomMargin { get; init; } = PdfRenderDefaults.DefaultPageBottomMargin;
        public Unit PageLeftMargin { get; init; } = PdfRenderDefaults.DefaultPageLeftMargin;
        public Unit PageRightMargin { get; init; } = PdfRenderDefaults.DefaultPageRightMargin;

        public IReadOnlyDictionary<string, string> FontAliases { get; init; } = PdfRenderDefaults.FontAliases;
        public IReadOnlyList<string> FontFallbackChain { get; init; } = PdfRenderDefaults.FontFallbackChain;
    }
}
