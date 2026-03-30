using MigraDoc.DocumentObjectModel;

namespace EIMSNext.Print.Test
{
    [TestClass]
    public class PdfImageRendererTests
    {
        private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aV7wAAAAASUVORK5CYII=";

        [TestMethod]
        public void RenderImages_ShouldAddImageFrame_WhenResourceIsValid()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook
            {
                Resources =
                [
                    new Pdf.UniverResource
                    {
                        Name = "logo",
                        Type = "image/png",
                        Data = TinyPngBase64
                    }
                ]
            };

            var worksheet = new Pdf.UniverWorksheet
            {
                Images =
                [
                    new Pdf.UniverImageData
                    {
                        ResourceId = "logo",
                        Position = new Pdf.UniverImagePosition
                        {
                            Left = 10,
                            Top = 20,
                            Width = 30,
                            Height = 40
                        }
                    }
                ]
            };

            using var temporaryFileSession = new Pdf.PdfTemporaryFileSession(options.TemporaryDirectory);
            var renderer = new Pdf.PdfImageRenderer(options, temporaryFileSession);
            var section = new Document().AddSection();

            renderer.RenderImages(section, workbook, worksheet);

            Assert.AreEqual(1, section.Elements.Count);
            Assert.IsInstanceOfType<MigraDoc.DocumentObjectModel.Shapes.TextFrame>(section.Elements[0]);
            Assert.AreEqual(1, temporaryFileSession.FilePaths.Count);
            Assert.IsTrue(File.Exists(temporaryFileSession.FilePaths[0]));
            StringAssert.Contains(temporaryFileSession.FilePaths[0], Path.Combine("eimsnext-print", "temp"));

            var frame = (MigraDoc.DocumentObjectModel.Shapes.TextFrame)section.Elements[0];
            Assert.IsTrue(frame.Width.Centimeter > 0);
            Assert.IsTrue(frame.Height.Centimeter > 0);
        }

        [TestMethod]
        public void RenderImages_ShouldIgnoreInvalidImageData()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook
            {
                Resources =
                [
                    new Pdf.UniverResource
                    {
                        Name = "broken",
                        Type = "image/png",
                        Data = "not-base64"
                    }
                ]
            };

            var worksheet = new Pdf.UniverWorksheet
            {
                Images =
                [
                    new Pdf.UniverImageData
                    {
                        ResourceId = "broken",
                        Position = new Pdf.UniverImagePosition { Width = 10, Height = 10 }
                    }
                ]
            };

            using var temporaryFileSession = new Pdf.PdfTemporaryFileSession(options.TemporaryDirectory);
            var renderer = new Pdf.PdfImageRenderer(options, temporaryFileSession);
            var section = new Document().AddSection();

            renderer.RenderImages(section, workbook, worksheet);

            Assert.AreEqual(0, section.Elements.Count);
            Assert.AreEqual(0, temporaryFileSession.FilePaths.Count);
        }

        [TestMethod]
        public void MapHorizontalPixelsToCm_ShouldSkipHiddenColumns()
        {
            var worksheet = new Pdf.UniverWorksheet
            {
                DefaultColumnWidth = 100,
                ColumnData = new Dictionary<string, Pdf.UniverColumnData>
                {
                    ["1"] = new() { Hidden = 1 }
                }
            };

            var calculator = new Pdf.PdfSheetLayoutCalculator(new Pdf.PdfRenderOptions());
            var visibleOnly = calculator.MapHorizontalPixelsToCm(worksheet, 200, 1.0);
            var allVisible = calculator.MapHorizontalPixelsToCm(new Pdf.UniverWorksheet { DefaultColumnWidth = 100 }, 200, 1.0);

            Assert.IsTrue(visibleOnly < allVisible);
        }

        [TestMethod]
        public void TemporaryFileSession_ShouldDeleteTrackedFiles_OnDispose()
        {
            var filePath = string.Empty;
            var tempDirectory = Path.Combine(Path.GetTempPath(), "eimsnext-print", "temp");

            using (var temporaryFileSession = new Pdf.PdfTemporaryFileSession(tempDirectory))
            {
                filePath = temporaryFileSession.CreateFile("txt", "demo"u8.ToArray());
                Assert.IsTrue(File.Exists(filePath));
                StringAssert.Contains(filePath, Path.Combine("eimsnext-print", "temp"));
            }

            Assert.IsFalse(File.Exists(filePath));
        }
    }
}
