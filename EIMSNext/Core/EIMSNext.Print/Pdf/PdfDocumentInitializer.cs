using MigraDoc;
using MigraDoc.DocumentObjectModel;
using PdfSharp.Fonts;

namespace EIMSNext.Print.Pdf
{
    internal static class PdfDocumentInitializer
    {
        public static void InitializeFonts(PdfRenderOptions renderOptions)
        {
            FontsCache.Initialize();
            PdfFontResolverRuntime.Configure(renderOptions);
            PredefinedFontsAndChars.ErrorFontName = renderOptions.DefaultFontFamily;
            GlobalFontSettings.FontResolver = new FontResolver();
            GlobalFontSettings.FallbackFontResolver = new FallbackFontResolver();
        }

        public static void InitializeDocumentDefaults(Document document, PdfRenderOptions renderOptions)
        {
            var normalStyle = document.Styles[StyleNames.Normal]!;
            normalStyle.Font.Name = renderOptions.DefaultFontFamily;
            normalStyle.Font.Size = renderOptions.DefaultFontSize;
            normalStyle.Font.Color = renderOptions.DefaultFontColor;
        }
    }
}
