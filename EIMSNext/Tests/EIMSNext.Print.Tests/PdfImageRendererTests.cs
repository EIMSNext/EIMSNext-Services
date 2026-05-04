using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Text.Json;

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

            var renderer = new Pdf.PdfImageRenderer(options);
            var section = new Document().AddSection();

            renderer.RenderImages(section, workbook, worksheet);

            Assert.AreEqual(1, section.Elements.Count);
            Assert.IsInstanceOfType<MigraDoc.DocumentObjectModel.Shapes.TextFrame>(section.Elements[0]);
            var frame = (MigraDoc.DocumentObjectModel.Shapes.TextFrame)section.Elements[0];
            Assert.IsTrue(frame.Width.Centimeter > 0);
            Assert.IsTrue(frame.Height.Centimeter > 0);
            Assert.AreEqual(MigraDoc.DocumentObjectModel.Shapes.RelativeHorizontal.Page, frame.RelativeHorizontal);
            Assert.AreEqual(MigraDoc.DocumentObjectModel.Shapes.RelativeVertical.Page, frame.RelativeVertical);
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

            var renderer = new Pdf.PdfImageRenderer(options);
            var section = new Document().AddSection();

            renderer.RenderImages(section, workbook, worksheet);

            Assert.AreEqual(0, section.Elements.Count);
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

            var renderer = new Pdf.PdfImageRenderer(options);
            var section = new Document().AddSection();

            renderer.RenderImages(section, workbook, worksheet);

            Assert.AreEqual(1, section.Elements.Count);
        }

        [TestMethod]
        public void RenderImages_ShouldUseConfiguredRowHeightForVerticalOffset()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook();
            var worksheet = new Pdf.UniverWorksheet
            {
                DefaultRowHeight = 20,
                RowData = new Dictionary<string, Pdf.UniverRowData>
                {
                    ["0"] = new() { Height = 20, ActualHeight = 60 }
                },
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
                                ColumnOffset = 0,
                                Row = 1,
                                RowOffset = 0,
                            },
                            To = new Pdf.UniverGridAnchor
                            {
                                Column = 0,
                                ColumnOffset = 20,
                                Row = 1,
                                RowOffset = 20,
                            }
                        }
                    }
                ]
            };

            var renderer = new Pdf.PdfImageRenderer(options);
            var document = new Document();
            var section = document.AddSection();

            renderer.RenderImages(section, workbook, worksheet);

            var frame = (MigraDoc.DocumentObjectModel.Shapes.TextFrame)section.Elements[0];
            var expectedTop = section.PageSetup.TopMargin.Centimeter + Pdf.PdfConvertUtil.PixelToCm(20, 0);
            Assert.AreEqual(expectedTop, frame.Top.Position.Centimeter, 0.0001);
        }

        [TestMethod]
        public void TryRenderCellImage_ShouldRenderInlineImgPayload()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook();
            var worksheet = new Pdf.UniverWorksheet();
            var renderer = new Pdf.PdfImageRenderer(options);
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
        public void TryRenderCellImage_ShouldResolveImageIdFromWorkbookResources()
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
                          "images": {
                            "image-1": {
                              "imageId": "image-1",
                              "source": "data:image/png;base64,{{TinyPngBase64}}"
                            }
                          }
                        }
                        """
                    }
                ]
            };
            var worksheet = new Pdf.UniverWorksheet();
            var renderer = new Pdf.PdfImageRenderer(options);
            var table = new Table();
            table.AddColumn();
            var row = table.AddRow();
            var cell = row.Cells[0];

            var univerCell = new Pdf.UniverCell
            {
                InlineImage = new Pdf.UniverCellImage
                {
                    ImageId = "image-1",
                    Width = 24,
                    Height = 24,
                }
            };

            var rendered = renderer.TryRenderCellImage(cell, workbook, worksheet, univerCell, 0, 0, 1.0);

            Assert.IsTrue(rendered);
            Assert.AreEqual(1, cell.Elements.Count);
        }

        [TestMethod]
        public void TryRenderCellImage_ShouldExtractImageFromParagraphDrawings()
        {
            var options = new Pdf.PdfRenderOptions();
            var workbook = new Pdf.UniverWorkbook();
            var worksheet = new Pdf.UniverWorksheet();
            var renderer = new Pdf.PdfImageRenderer(options);
            var table = new Table();
            table.AddColumn();
            var row = table.AddRow();
            var cell = row.Cells[0];

            var univerCell = JsonSerializer.Deserialize<Pdf.UniverCell>(
                $$"""
                {
                  "p": {
                    "drawings": {
                      "drawing-1": {
                        "drawingId": "drawing-1",
                        "imageSourceType": "BASE64",
                        "source": "data:image/png;base64,{{TinyPngBase64}}",
                        "transform": {
                          "width": 97,
                          "height": 24
                        }
                      }
                    }
                  }
                }
                """,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            var rendered = renderer.TryRenderCellImage(cell, workbook, worksheet, univerCell, 0, 0, 1.0);

            Assert.IsTrue(rendered);
            Assert.AreEqual(1, cell.Elements.Count);
        }

        [TestMethod]
        public void PdfGenerator_Preview_ShouldEmbedImage_WhenCellContainsParagraphDrawing()
        {
            var template = new EIMSNext.Print.Abstractions.PrintTemplate
            {
                Content = $$"""
                {
                  "id": "Sheet1",
                  "name": "Sheet1",
                  "sheetOrder": ["sheet-1"],
                  "sheets": {
                    "sheet-1": {
                      "id": "sheet-1",
                      "name": "Sheet1",
                      "defaultColumnWidth": 88,
                      "defaultRowHeight": 24,
                      "columnData": {
                        "0": { "w": 143 }
                      },
                      "rowData": {
                        "8": { "h": 45 }
                      },
                      "cellData": {
                        "8": {
                          "0": {
                            "p": {
                              "id": "d",
                              "drawings": {
                                "drawing-1": {
                                  "drawingId": "drawing-1",
                                  "imageSourceType": "BASE64",
                                  "source": "data:image/png;base64,{{TinyPngBase64}}",
                                  "transform": {
                                    "width": 97,
                                    "height": 24
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
                """
            };

            var result = new Pdf.PdfGenerator().Preview(template, new EIMSNext.Print.Abstractions.PrintOption());
            Assert.IsTrue(result.Content.Length > 0);

            using var memory = new MemoryStream();
            result.Content.CopyTo(memory);
            memory.Position = 0;

            using var document = PdfReader.Open(memory, PdfDocumentOpenMode.Import);
            Assert.AreEqual(1, document.PageCount);

            var page = document.Pages[0];
            var resources = page.Elements.GetDictionary("/Resources");
            Assert.IsNotNull(resources);

            Assert.IsTrue(resources!.Elements.Count > 0);
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
        public void MapVerticalPixelsToCm_ShouldPreferConfiguredRowHeightOverActualHeight()
        {
            var worksheet = new Pdf.UniverWorksheet
            {
                DefaultRowHeight = 20,
                RowData = new Dictionary<string, Pdf.UniverRowData>
                {
                    ["0"] = new() { Height = 20, ActualHeight = 60 }
                }
            };

            var calculator = new Pdf.PdfSheetLayoutCalculator(new Pdf.PdfRenderOptions());
            var mapped = calculator.MapVerticalPixelsToCm(worksheet, 20, 1.0);

            Assert.AreEqual(Pdf.PdfConvertUtil.PixelToCm(20, 0), mapped, 0.0001);
        }

    }
}
