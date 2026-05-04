using System.Text.Json.Nodes;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace EIMSNext.Print.Tests
{
    [TestClass]
    public class PdfTableGeneratorTests
    {
        [TestMethod]
        public void Generate_ShouldKeepEmptyRepeatRow_WhenDetailDataIsMissing()
        {
            var workbook = new Pdf.UniverWorkbook
            {
                Styles = new Dictionary<string, Pdf.UniverStyle>(),
                Sheets = new Dictionary<string, Pdf.UniverWorksheet>
                {
                    ["sheet1"] = new Pdf.UniverWorksheet
                    {
                        DefaultColumnWidth = 80,
                        DefaultRowHeight = 20,
                        CellData = new Dictionary<string, Dictionary<string, Pdf.UniverCell>>
                        {
                            ["0"] = new()
                            {
                                ["0"] = new Pdf.UniverCell { Value = "Header" }
                            },
                            ["1"] = new()
                            {
                                ["0"] = new Pdf.UniverCell
                                {
                                    PrintMeta = new Pdf.PdfPrintMeta { Id = "details>name" }
                                }
                            },
                            ["2"] = new()
                            {
                                ["0"] = new Pdf.UniverCell { Value = "Footer" }
                            }
                        }
                    }
                }
            };

            var worksheet = workbook.Sheets["sheet1"];
            var document = new Document();
            var section = document.AddSection();
            var table = section.AddTable();
            var generator = new Pdf.PdfTableGenerator(workbook, new Pdf.PdfRenderOptions());
            var data = JsonNode.Parse("{\"details\":[]}")!.AsObject();

            generator.Generate(worksheet, table, data, section.PageSetup);

            Assert.AreEqual(3, table.Rows.Count);
            Assert.AreEqual(1, table.Rows[1].Cells[0].Elements.Count);
            Assert.IsInstanceOfType<MigraDoc.DocumentObjectModel.Paragraph>(table.Rows[1].Cells[0].Elements[0]);
        }

        [TestMethod]
        public void Generate_ShouldKeepRepeatRowStyle_WhenDetailDataIsMissing()
        {
            var workbook = new Pdf.UniverWorkbook
            {
                Styles = new Dictionary<string, Pdf.UniverStyle>
                {
                    ["detailStyle"] = new()
                    {
                        Background = new Pdf.UniverColor { Rgb = "#eeeeee" },
                        Color = new Pdf.UniverColor { Rgb = "#333333" },
                        Border = new Pdf.UniverBorder
                        {
                            Bottom = new Pdf.UniverBorderSide { Style = 1, Color = new Pdf.UniverColor { Rgb = "#000000" } }
                        }
                    }
                },
                Sheets = new Dictionary<string, Pdf.UniverWorksheet>
                {
                    ["sheet1"] = new Pdf.UniverWorksheet
                    {
                        DefaultColumnWidth = 80,
                        DefaultRowHeight = 20,
                        CellData = new Dictionary<string, Dictionary<string, Pdf.UniverCell>>
                        {
                            ["0"] = new()
                            {
                                ["0"] = new Pdf.UniverCell { Value = "Header" }
                            },
                            ["1"] = new()
                            {
                                ["0"] = new Pdf.UniverCell
                                {
                                    Style = "detailStyle",
                                    PrintMeta = new Pdf.PdfPrintMeta { Id = "details>name" }
                                }
                            }
                        }
                    }
                }
            };

            var worksheet = workbook.Sheets["sheet1"];
            var document = new Document();
            var section = document.AddSection();
            var table = section.AddTable();
            var generator = new Pdf.PdfTableGenerator(workbook, new Pdf.PdfRenderOptions());
            var data = JsonNode.Parse("{\"details\":[]}")!.AsObject();

            generator.Generate(worksheet, table, data, section.PageSetup);

            Assert.AreEqual(2, table.Rows.Count);
            Assert.AreEqual(1, table.Rows[1].Cells[0].Elements.Count);
            Assert.AreEqual(Color.FromRgb(0xee, 0xee, 0xee), table.Rows[1].Cells[0].Shading.Color);
            Assert.AreEqual(Color.FromRgb(0x00, 0x00, 0x00), table.Rows[1].Cells[0].Borders.Bottom.Color);
        }

        [TestMethod]
        public void Generate_ShouldMarkDetailHeaderRow_WhenRepeatRowFollows()
        {
            var workbook = new Pdf.UniverWorkbook
            {
                Styles = new Dictionary<string, Pdf.UniverStyle>(),
                Sheets = new Dictionary<string, Pdf.UniverWorksheet>
                {
                    ["sheet1"] = new Pdf.UniverWorksheet
                    {
                        DefaultColumnWidth = 80,
                        DefaultRowHeight = 20,
                        CellData = new Dictionary<string, Dictionary<string, Pdf.UniverCell>>
                        {
                            ["0"] = new()
                            {
                                ["0"] = new Pdf.UniverCell { Value = "Header" }
                            },
                            ["1"] = new()
                            {
                                ["0"] = new Pdf.UniverCell
                                {
                                    PrintMeta = new Pdf.PdfPrintMeta { Id = "details>name" }
                                }
                            },
                            ["2"] = new()
                            {
                                ["0"] = new Pdf.UniverCell { Value = "Footer" }
                            }
                        }
                    }
                }
            };

            var worksheet = workbook.Sheets["sheet1"];
            var document = new Document();
            var section = document.AddSection();
            var table = section.AddTable();
            var generator = new Pdf.PdfTableGenerator(workbook, new Pdf.PdfRenderOptions());
            var data = JsonNode.Parse("{\"details\":[{\"name\":\"A\"}]}")!.AsObject();

            generator.Generate(worksheet, table, data, section.PageSetup);

            Assert.AreEqual(3, table.Rows.Count);
            Assert.IsTrue(table.Rows[0].HeadingFormat);
            Assert.AreEqual(1, table.Rows[0].KeepWith);
        }

        [TestMethod]
        public void Generate_ShouldRenderParagraphDrawingImageInsideCell()
        {
            const string tinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aV7wAAAAASUVORK5CYII=";

            var workbook = new Pdf.UniverWorkbook
            {
                Styles = new Dictionary<string, Pdf.UniverStyle>(),
                Sheets = new Dictionary<string, Pdf.UniverWorksheet>
                {
                    ["sheet1"] = new Pdf.UniverWorksheet
                    {
                        DefaultColumnWidth = 88,
                        DefaultRowHeight = 24,
                        ColumnData = new Dictionary<string, Pdf.UniverColumnData>
                        {
                            ["0"] = new() { Width = 143 }
                        },
                        RowData = new Dictionary<string, Pdf.UniverRowData>
                        {
                            ["8"] = new() { Height = 45 }
                        },
                        CellData = new Dictionary<string, Dictionary<string, Pdf.UniverCell>>
                        {
                            ["8"] = new()
                            {
                                ["0"] = System.Text.Json.JsonSerializer.Deserialize<Pdf.UniverCell>(
                                    $$"""
                                    {
                                      "p": {
                                        "drawings": {
                                          "drawing-1": {
                                            "drawingId": "drawing-1",
                                            "imageSourceType": "BASE64",
                                            "source": "data:image/png;base64,{{tinyPngBase64}}",
                                            "transform": {
                                              "width": 97,
                                              "height": 24
                                            }
                                          }
                                        }
                                      }
                                    }
                                    """,
                                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
                            }
                        }
                    }
                }
            };

            var worksheet = workbook.Sheets["sheet1"];
            var document = new Document();
            var section = document.AddSection();
            var table = section.AddTable();
            var generator = new Pdf.PdfTableGenerator(workbook, new Pdf.PdfRenderOptions(), true);
            var data = new JsonObject();

            generator.Generate(worksheet, table, data, section.PageSetup);

            Assert.AreEqual(9, table.Rows.Count);
            Assert.AreEqual(1, table.Rows[8].Cells[0].Elements.Count);
            Assert.IsInstanceOfType<Paragraph>(table.Rows[8].Cells[0].Elements[0]);

            var paragraph = (Paragraph)table.Rows[8].Cells[0].Elements[0];
            Assert.AreEqual(1, paragraph.Elements.Count);
            Assert.IsInstanceOfType<MigraDoc.DocumentObjectModel.Shapes.Image>(paragraph.Elements[0]);
        }
    }
}
