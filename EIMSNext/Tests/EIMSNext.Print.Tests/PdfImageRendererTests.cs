using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Tests
{
    [TestClass]
    public class PdfImageRendererTests
    {
        private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aV7wAAAAASUVORK5CYII=";

        [TestMethod]
        public void RenderImages_ShouldAddImageFrame_WhenResourceIsValid()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook();

            var worksheet = new Pdf.UniverWorksheet
            {
                Images =
                [
                    new Pdf.UniverImageData
                    {
                        Source = $"data:image/png;base64,{TinyPngBase64}",
                        ImageSourceType = "BASE64",
                        SheetTransform = new Pdf.UniverSheetTransform
                        {
                            From = new Pdf.UniverGridAnchor
                            {
                                Column = 0,
                                ColumnOffset = 10,
                                Row = 0,
                                RowOffset = 20,
                            },
                            To = new Pdf.UniverGridAnchor
                            {
                                Column = 0,
                                ColumnOffset = 40,
                                Row = 0,
                                RowOffset = 60,
                            }
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
            var workbook = new Pdf.UniverWorkbook();

            var worksheet = new Pdf.UniverWorksheet
            {
                Images =
                [
                    new Pdf.UniverImageData
                    {
                        Source = "data:image/png;base64,not-base64",
                        ImageSourceType = "BASE64",
                        SheetTransform = new Pdf.UniverSheetTransform
                        {
                            From = new Pdf.UniverGridAnchor(),
                            To = new Pdf.UniverGridAnchor
                            {
                                ColumnOffset = 10,
                                RowOffset = 10,
                            }
                        }
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
        public void RenderImages_ShouldReadFloatingImageFromDrawingResource()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook
            {
                Resources =
                [
                    new Pdf.UniverResource
                    {
                        Name = "SHEET_DRAWING_PLUGIN",
                        Data = $$"""
                        {
                          "Sheet1": {
                            "drawingData": {
                              "drawings": {
                                "drawing-1": {
                                  "drawingId": "drawing-1",
                                  "imageId": "image-1",
                                  "source": "data:image/png;base64,{{TinyPngBase64}}",
                                  "imageSourceType": "BASE64",
                                  "width": 30,
                                  "height": 40,
                                  "sheetTransform": {
                                    "from": { "row": 0, "rowOffset": 0, "column": 0, "columnOffset": 0 },
                                    "to": { "row": 1, "rowOffset": 0, "column": 1, "columnOffset": 0 }
                                  },
                                  "axisAlignSheetTransform": {
                                    "from": { "row": 0, "rowOffset": 0, "column": 0, "columnOffset": 0 },
                                    "to": { "row": 1, "rowOffset": 0, "column": 1, "columnOffset": 0 }
                                  }
                                }
                              }
                            }
                          }
                        }
                        """
                    }
                ]
            };

            var worksheet = new Pdf.UniverWorksheet
            {
                Id = "Sheet1",
                Name = "Sheet1"
            };

            using var temporaryFileSession = new Pdf.PdfTemporaryFileSession(options.TemporaryDirectory);
            var renderer = new Pdf.PdfImageRenderer(options, temporaryFileSession);
            var section = new Document().AddSection();

            renderer.RenderImages(section, workbook, worksheet);

            Assert.AreEqual(1, section.Elements.Count);
            Assert.AreEqual(1, temporaryFileSession.FilePaths.Count);
        }

        [TestMethod]
        public void TryRenderCellImage_ShouldRenderInlineImgPayload()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook();
            var worksheet = new Pdf.UniverWorksheet();
            var renderer = new Pdf.PdfImageRenderer(options, new Pdf.PdfTemporaryFileSession(options.TemporaryDirectory));
            var table = new Table();
            table.AddColumn();
            var row = table.AddRow();
            var cell = row.Cells[0];

            var univerCell = new Pdf.UniverCell
            {
                InlineImage = new Pdf.UniverCellImage
                {
                    Source = $"data:image/png;base64,{TinyPngBase64}",
                    ImageSourceType = "BASE64",
                    Width = 24,
                    Height = 24,
                }
            };

            var rendered = renderer.TryRenderCellImage(cell, workbook, worksheet, univerCell, 0, 0, 1.0);

            Assert.IsTrue(rendered);
            Assert.AreEqual(1, cell.Elements.Count);
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
