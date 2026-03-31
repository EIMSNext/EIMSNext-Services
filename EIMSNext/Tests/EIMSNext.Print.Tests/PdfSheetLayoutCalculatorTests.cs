using MigraDoc.DocumentObjectModel;

namespace EIMSNext.Print.Tests
{
    [TestClass]
    public class PdfSheetLayoutCalculatorTests
    {
        [TestMethod]
        public void CreatePlan_ShouldSkipHiddenRowsAndColumns_AndKeepVisibleMerge()
        {
            var worksheet = new Pdf.UniverWorksheet
            {
                DefaultColumnWidth = 80,
                DefaultRowHeight = 20,
                CellData = new Dictionary<string, Dictionary<string, Pdf.UniverCell>>
                {
                    ["0"] = new()
                    {
                        ["0"] = new Pdf.UniverCell { Value = "A" },
                        ["1"] = new Pdf.UniverCell { Value = "B" },
                        ["2"] = new Pdf.UniverCell { Value = "C" }
                    },
                    ["1"] = new()
                    {
                        ["0"] = new Pdf.UniverCell { Value = "D" }
                    }
                },
                RowData = new Dictionary<string, Pdf.UniverRowData>
                {
                    ["1"] = new() { Hidden = 1 }
                },
                ColumnData = new Dictionary<string, Pdf.UniverColumnData>
                {
                    ["2"] = new() { Hidden = 1 }
                },
                MergeData =
                [
                    new Pdf.UniverRange { StartRow = 0, EndRow = 0, StartColumn = 0, EndColumn = 1 }
                ]
            };

            var calculator = new Pdf.PdfSheetLayoutCalculator(new Pdf.PdfRenderOptions());
            var document = new Document();
            var section = document.AddSection();

            var plan = calculator.CreatePlan(worksheet, section.PageSetup);

            CollectionAssert.AreEqual(new[] { 0 }, plan.VisibleRows.ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1 }, plan.VisibleColumns.ToArray());
            Assert.IsTrue(plan.MergeCells.TryGetValue((0, 0), out var mergePlan));
            Assert.IsTrue(mergePlan!.IsMasterCell);
            Assert.AreEqual(1, mergePlan.MergeRight);
        }
    }
}
