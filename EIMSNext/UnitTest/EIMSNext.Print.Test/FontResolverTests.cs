namespace EIMSNext.Print.Test
{
    [TestClass]
    public class FontResolverTests
    {
        [TestMethod]
        public void ResolveFontFamily_ShouldMapChineseAlias()
        {
            Pdf.PdfFontResolverRuntime.Configure(new Pdf.PdfRenderOptions());

            var family = Pdf.PdfFontResolverRuntime.ResolveFontFamily("宋体", false, false);

            Assert.AreEqual("SimSun", family);
        }

        [TestMethod]
        public void ResolveFontFamily_ShouldFallbackToDefaultFont()
        {
            Pdf.PdfFontResolverRuntime.Configure(new Pdf.PdfRenderOptions());

            var family = Pdf.PdfFontResolverRuntime.ResolveFontFamily("Unknown Font Family", false, false);

            Assert.AreEqual(Pdf.PdfRenderDefaults.DefaultFontFamily, family);
        }
    }
}
