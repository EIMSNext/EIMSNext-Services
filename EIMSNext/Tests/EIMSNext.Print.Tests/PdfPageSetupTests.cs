using System.Reflection;
using MigraDoc.DocumentObjectModel;

namespace EIMSNext.Print.Tests
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
                    PaperSize = "letter",
                    TopMargin = 72,
                    BottomMargin = 36,
                    LeftMargin = 18,
                    RightMargin = 24
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

        [TestMethod]
        public void DeserializeWorkbook_ShouldIgnoreLegacyPrintSetupAndLegacyPageFields()
        {
            var json = """
            {
              "id": "Sheet1",
              "name": "Sheet1",
              "sheetOrder": ["Sheet1"],
              "sheets": {
                "Sheet1": {
                  "id": "Sheet1",
                  "name": "Sheet1",
                  "printSetup": {
                    "paperSize": "Letter",
                    "topMargin": 99
                  },
                  "pageSetup": {
                    "paperSize": "A5",
                    "pageSize": "Letter",
                    "pageFormat": "Legal",
                    "marginTop": 91,
                    "marginBottom": 92,
                    "marginLeft": 93,
                    "marginRight": 94,
                    "topMargin": 72,
                    "bottomMargin": 36,
                    "leftMargin": 18,
                    "rightMargin": 24
                  }
                }
              }
            }
            """;

            var workbook = System.Text.Json.JsonSerializer.Deserialize<Pdf.UniverWorkbook>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            Assert.IsNotNull(workbook);
            var worksheet = workbook!.Sheets["Sheet1"];
            Assert.IsNotNull(worksheet.PageSetup);
            Assert.AreEqual("A5", worksheet.PageSetup!.PaperSize);
            Assert.AreEqual(72, worksheet.PageSetup.TopMargin);
            Assert.AreEqual(36, worksheet.PageSetup.BottomMargin);
            Assert.AreEqual(18, worksheet.PageSetup.LeftMargin);
            Assert.AreEqual(24, worksheet.PageSetup.RightMargin);
        }

        [TestMethod]
        public void ApplyPageSetup_ShouldNotReadLegacyPrintSetup()
        {
            var section = new Document().AddSection();
            var options = new Pdf.PdfRenderOptions();
            var worksheet = System.Text.Json.JsonSerializer.Deserialize<Pdf.UniverWorksheet>("""
            {
              "id": "Sheet1",
              "name": "Sheet1",
              "printSetup": {
                "paperSize": "Letter",
                "topMargin": 72,
                "bottomMargin": 36,
                "leftMargin": 18,
                "rightMargin": 24
              }
            }
            """, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            var method = typeof(Pdf.PdfGenerator).GetMethod("ApplyPageSetup", BindingFlags.NonPublic | BindingFlags.Static);
            method!.Invoke(null, new object[] { section, worksheet!, options });

            Assert.AreEqual(options.PageFormat, section.PageSetup.PageFormat);
            Assert.AreEqual(options.PageTopMargin, section.PageSetup.TopMargin);
            Assert.AreEqual(options.PageBottomMargin, section.PageSetup.BottomMargin);
            Assert.AreEqual(options.PageLeftMargin, section.PageSetup.LeftMargin);
            Assert.AreEqual(options.PageRightMargin, section.PageSetup.RightMargin);
        }
    }
}
