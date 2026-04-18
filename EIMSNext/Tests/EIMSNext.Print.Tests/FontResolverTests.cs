namespace EIMSNext.Print.Tests
{
    [TestClass]
    public class FontResolverTests
    {
        [TestMethod]
        public void ResolveFontFamily_ShouldMapChineseAlias()
        {
            Pdf.FontsCache.Initialize();
            Pdf.PdfFontResolverRuntime.Configure(new Pdf.PdfRenderOptions());

            var family = Pdf.PdfFontResolverRuntime.ResolveFontFamily("宋体", false, false);

            Assert.AreEqual("SimSun", family);
        }

        [TestMethod]
        public void ResolveFontFamily_ShouldFallbackToDefaultFont()
        {
            Pdf.FontsCache.Initialize();
            Pdf.PdfFontResolverRuntime.Configure(new Pdf.PdfRenderOptions());

            var family = Pdf.PdfFontResolverRuntime.ResolveFontFamily("Unknown Font Family", false, false);

            Assert.AreEqual(Pdf.PdfRenderDefaults.DefaultFontFamily, family);
        }

        [TestMethod]
        public void ResolveFontFamily_ShouldKeepYaHeiWithinUiFamily_WhenItalicRequested()
        {
            Pdf.FontsCache.Initialize();
            Pdf.PdfFontResolverRuntime.Configure(new Pdf.PdfRenderOptions());

            var family = Pdf.PdfFontResolverRuntime.ResolveFontFamily("Microsoft YaHei", false, true);

            StringAssert.Contains(family, "Microsoft YaHei UI");
        }

        [TestMethod]
        public void ResolveFontFamily_ShouldKeepFangSongWithinFamily_WhenItalicRequested()
        {
            Pdf.FontsCache.Initialize();
            Pdf.PdfFontResolverRuntime.Configure(new Pdf.PdfRenderOptions());

            var family = Pdf.PdfFontResolverRuntime.ResolveFontFamily("fangsong", false, true);

            StringAssert.Contains(family.ToLowerInvariant(), "fangsong");
        }
    }
}
