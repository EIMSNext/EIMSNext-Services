using System.Text.Json.Nodes;
using MigraDoc.DocumentObjectModel;

namespace EIMSNext.Print.Test
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
    }
}
