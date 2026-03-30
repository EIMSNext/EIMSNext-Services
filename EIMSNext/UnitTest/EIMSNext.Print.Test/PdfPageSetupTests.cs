using System.Reflection;
using MigraDoc.DocumentObjectModel;

namespace EIMSNext.Print.Test
{
    [TestClass]
    public class PdfPageSetupTests
    {
        [TestMethod]
        public void ApplyPageSetup_ShouldUseWorksheetPageSetup_WhenProvided()
        {
            var section = new Document().AddSection();
            var worksheet = new Pdf.UniverWorksheet
            {
                PageSetup = new Pdf.UniverPageSetup
                {
                    Orientation = "landscape",
                    PageFormat = "letter",
                    MarginTop = 72,
                    MarginBottom = 36,
                    MarginLeft = 18,
                    MarginRight = 24
                }
            };

            var method = typeof(Pdf.PdfGenerator).GetMethod("ApplyPageSetup", BindingFlags.NonPublic | BindingFlags.Static);
            method!.Invoke(null, new object[] { section, worksheet, new Pdf.PdfRenderOptions() });

            Assert.AreEqual(PageFormat.Letter, section.PageSetup.PageFormat);
            Assert.AreEqual(Orientation.Landscape, section.PageSetup.Orientation);
            Assert.AreEqual(Unit.FromPoint(72), section.PageSetup.TopMargin);
            Assert.AreEqual(Unit.FromPoint(36), section.PageSetup.BottomMargin);
            Assert.AreEqual(Unit.FromPoint(18), section.PageSetup.LeftMargin);
            Assert.AreEqual(Unit.FromPoint(24), section.PageSetup.RightMargin);
        }

        [TestMethod]
        public void ApplyPageSetup_ShouldFallbackToDefaults_WhenWorksheetPageSetupMissing()
        {
            var section = new Document().AddSection();
            var worksheet = new Pdf.UniverWorksheet();
            var options = new Pdf.PdfRenderOptions();

            var method = typeof(Pdf.PdfGenerator).GetMethod("ApplyPageSetup", BindingFlags.NonPublic | BindingFlags.Static);
            method!.Invoke(null, new object[] { section, worksheet, options });

            Assert.AreEqual(options.PageFormat, section.PageSetup.PageFormat);
            Assert.AreEqual(options.Orientation, section.PageSetup.Orientation);
            Assert.AreEqual(options.PageTopMargin, section.PageSetup.TopMargin);
            Assert.AreEqual(options.PageBottomMargin, section.PageSetup.BottomMargin);
        }
    }
}
