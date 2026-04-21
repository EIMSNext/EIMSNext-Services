using System.Text;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Async.Tasks.Export;
using EIMSNext.Auth.Entities;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;
using HKH.CSV;
using NPOI.XSSF.UserModel;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class ExportFileBuilderTests
    {
        [TestMethod]
        public void WriteCsv_ShouldUseUtf8WithoutBom()
        {
            var bytes = ExportFileBuilder.WriteCsv(
            [
                ["标题1", "标题2"],
                ["a", "b"]
            ]);

            CollectionAssert.AreNotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        }

        [TestMethod]
        public void WriteCsv_ShouldEscapeCommaQuoteAndNewLine()
        {
            var bytes = ExportFileBuilder.WriteCsv(
            [
                ["col1", "col2"],
                ["a,b", "x\"y"],
                ["line1\nline2", "plain"]
            ]);

            var csvText = Encoding.UTF8.GetString(bytes);
            StringAssert.Contains(csvText, "\"a,b\"");
            StringAssert.Contains(csvText, "\"x\"\"y\"");

            using var ms = new MemoryStream(bytes);
            using var reader = new CSVReader(ms, ',', '"', '\\', false, true, 0, false, false);
            var rows = reader.ReadAll().Select(x => x.ToArray()).ToList();

            Assert.IsTrue(rows.Count >= 2);
            CollectionAssert.AreEqual(new[] { "a,b", "x\"y" }, rows[1]);

            var combined = string.Join("\n", rows.Skip(2).SelectMany(x => x));
            StringAssert.Contains(combined, "line1");
            StringAssert.Contains(combined, "line2");
            StringAssert.Contains(combined, "plain");
        }

        [TestMethod]
        public void GetAuditLoginCellValue_ShouldPreventFormulaInjection()
        {
            var column = new ExportColumn { Key = "userName", Header = "登录人", Type = ExportColumnType.String };
            var row = new AuditLogin { UserName = "=cmd|' /C calc'!A0" };

            var value = AuditLoginExportProcessor.GetCellValue(column, row);

            Assert.AreEqual("'=cmd|' /C calc'!A0", value.Text);
        }

        [TestMethod]
        public void BuildAuditLogExcel_ShouldWriteDateAndStringCells()
        {
            var columns = new List<ExportColumn>
            {
                new() { Key = "createTime", Header = "操作时间", Type = ExportColumnType.Date },
                new() { Key = "detail", Header = "操作详情", Type = ExportColumnType.String },
            };

            var rows = new List<AuditLog>
            {
                new()
                {
                    CreateTime = DateTimeOffset.Parse("2026-04-14 09:30:45 +08:00").ToUnixTimeMilliseconds(),
                    Detail = "detail text",
                }
            };

            using var workbookBuilder = ExportFileBuilder.CreateWorkbook();
            var sheet = ExportFileBuilder.InitializeExcelSheet(workbookBuilder, "操作日志", columns, out var styles);
            AuditLogExportProcessor.WriteExcelRows(sheet, styles, columns, rows, 1);

            using var ms = new MemoryStream();
            workbookBuilder.Write(ms, false);
            workbookBuilder.Dispose();
            var bytes = ms.ToArray();

            using var workbook = new XSSFWorkbook(new MemoryStream(bytes));
            var outputSheet = workbook.GetSheetAt(0);
            var dataRow = outputSheet.GetRow(1);

            Assert.IsNotNull(dataRow);
            Assert.AreEqual("detail text", dataRow.GetCell(1).StringCellValue);
            Assert.AreEqual("yyyy-MM-dd HH:mm:ss", dataRow.GetCell(0).CellStyle.GetDataFormatString());
        }

        [TestMethod]
        public void WriteExcelCell_ShouldWriteNumberWithTwoDecimalsFormat()
        {
            using var workbook = new XSSFWorkbook();
            var styles = ExportFileBuilder.CreateExcelStyles(workbook);
            var sheet = workbook.CreateSheet("Sheet1");
            var row = sheet.CreateRow(0);
            var cell = row.CreateCell(0);

            ExportFileBuilder.WriteExcelCell(cell, new ExportFileBuilder.ExportCellValue
            {
                Type = ExportColumnType.Number,
                Number = 12.345m,
            }, styles);

            Assert.AreEqual(12.345d, cell.NumericCellValue, 0.0001d);
            Assert.AreEqual("0.00", cell.CellStyle.GetDataFormatString());
        }

        [TestMethod]
        public void BuildAuditLoginExcel_ShouldFreezeFirstRow()
        {
            var columns = new List<ExportColumn>
            {
                new() { Key = "userName", Header = "登录人", Type = ExportColumnType.String }
            };
            var rows = new List<AuditLogin> { new() { UserName = "Alice" } };

            using var workbookBuilder = ExportFileBuilder.CreateWorkbook();
            var sheet = ExportFileBuilder.InitializeExcelSheet(workbookBuilder, "登录日志", columns, out var styles);
            AuditLoginExportProcessor.WriteExcelRows(sheet, styles, columns, rows, 1);

            using var ms = new MemoryStream();
            workbookBuilder.Write(ms, false);
            workbookBuilder.Dispose();
            var bytes = ms.ToArray();

            using var workbook = new XSSFWorkbook(new MemoryStream(bytes));
            var pane = workbook.GetSheetAt(0).PaneInformation;

            Assert.IsNotNull(pane);
            Assert.AreEqual((short)1, pane.HorizontalSplitTopRow);
        }

        [TestMethod]
        public async Task ExportFileResult_DisposeAsync_ShouldDeleteTempFile()
        {
            var fileName = "cleanup-test.csv";
            var tempFile = ExportProcessorBase.CreateTempFilePath(fileName);
            await File.WriteAllTextAsync(tempFile, "header\r\nvalue");

            Assert.IsTrue(File.Exists(tempFile));

            await using (var result = new ExportFileBuilder.ExportFileResult
            {
                FileName = fileName,
                Content = ExportProcessorBase.OpenTempFileForRead(tempFile),
                TotalCount = 1,
            })
            {
                Assert.IsTrue(File.Exists(tempFile));
                using var reader = new StreamReader(result.Content, Encoding.UTF8, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                StringAssert.Contains(content, "value");
            }

            Assert.IsFalse(File.Exists(tempFile));
        }
    }
}
