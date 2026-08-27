using System.Dynamic;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Async.Tasks.Consumers;
using EIMSNext.Entities;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class FormDataImportErrorReportBuilderTests
    {
        // ===== BuildTaskFailureReport =====

        [TestMethod]
        public void BuildTaskFailureReport_ProducesXlsxWithFormFileAndReason()
        {
            var log = new FormDataImportLog
            {
                Id = "log-1",
                FormId = "form-1",
                FormName = "客户登记表",
                SourceFileName = "data.xlsx",
            };
            var ex = new InvalidOperationException("源文件不存在");

            var report = DataImportConsumer.BuildTaskFailureReport(log, ex);

            Assert.IsTrue(report.FileName.StartsWith("导入失败报告_"));
            Assert.IsNotNull(report.Content);
            Assert.IsTrue(report.Content.Length > 0);

            // 反向解析
            using var ms = new MemoryStream(report.Content);
            var wb = new XSSFWorkbook(ms);
            var sheet = wb.GetSheetAt(0);
            Assert.AreEqual("导入失败", sheet.SheetName);

            var header = sheet.GetRow(0);
            Assert.AreEqual("表单", GetCellText(header.GetCell(0)));
            Assert.AreEqual("源文件", GetCellText(header.GetCell(1)));
            Assert.AreEqual("失败原因", GetCellText(header.GetCell(2)));

            var row = sheet.GetRow(1);
            Assert.AreEqual("客户登记表", GetCellText(row.GetCell(0)));
            Assert.AreEqual("data.xlsx", GetCellText(row.GetCell(1)));
            Assert.AreEqual("源文件不存在", GetCellText(row.GetCell(2)));
        }

        [TestMethod]
        public void BuildTaskFailureReport_FallsBackToFormIdWhenFormNameMissing()
        {
            var log = new FormDataImportLog { Id = "log-1", FormId = "form-1" };
            var ex = new Exception("err");

            var report = DataImportConsumer.BuildTaskFailureReport(log, ex);

            using var ms = new MemoryStream(report.Content);
            var wb = new XSSFWorkbook(ms);
            var row = wb.GetSheetAt(0).GetRow(1);
            Assert.AreEqual("form-1", GetCellText(row.GetCell(0)));
        }

        // ===== BuildErrorReport =====

        [TestMethod]
        public void BuildErrorReport_ProducesXlsxWithRowAndErrorColumns()
        {
            var rows = new List<DataImportConsumer.ImportRecord>
            {
                new()
                {
                    RecordIndex = 1,
                    StartRowNumber = 2,
                    Data = NewExpando(new Dictionary<string, object?> { ["name"] = "Alice" }),
                    Errors = new List<FormDataImportCellError>
                    {
                        new() { FieldTitle = "姓名", Message = "必填字段不能为空" }
                    }
                },
                new()
                {
                    RecordIndex = 2,
                    StartRowNumber = 3,
                    EndRowNumber = 5,
                    Data = NewExpando(new Dictionary<string, object?> { ["name"] = "Bob" }),
                    Errors = new List<FormDataImportCellError>
                    {
                        new() { FieldTitle = "年龄", Message = "数字格式无效" }
                    }
                },
            };

            var report = DataImportConsumer.FormDataImportProcessor.BuildErrorReport(rows);

            Assert.IsTrue(report.FileName.StartsWith("导入错误报告_"));
            Assert.IsTrue(report.Content.Length > 0);

            using var ms = new MemoryStream(report.Content);
            var wb = new XSSFWorkbook(ms);
            var sheet = wb.GetSheetAt(0);
            Assert.AreEqual("错误数据", sheet.SheetName);

            var header = sheet.GetRow(0);
            Assert.AreEqual("行号", GetCellText(header.GetCell(0)));
            Assert.AreEqual("错误详情", GetCellText(header.GetCell(1)));
            Assert.AreEqual("数据", GetCellText(header.GetCell(2)));

            var r1 = sheet.GetRow(1);
            Assert.AreEqual("2", GetCellText(r1.GetCell(0)));
            Assert.AreEqual("姓名: 必填字段不能为空", GetCellText(r1.GetCell(1)));

            var r2 = sheet.GetRow(2);
            Assert.AreEqual("3-5", GetCellText(r2.GetCell(0)));
            Assert.AreEqual("年龄: 数字格式无效", GetCellText(r2.GetCell(1)));
        }

        [TestMethod]
        public void BuildErrorReport_EmptyRows_ProducesOnlyHeader()
        {
            var report = DataImportConsumer.FormDataImportProcessor.BuildErrorReport([]);
            using var ms = new MemoryStream(report.Content);
            var wb = new XSSFWorkbook(ms);
            var sheet = wb.GetSheetAt(0);
            Assert.IsNotNull(sheet.GetRow(0), "header row should exist");
            Assert.IsTrue(sheet.LastRowNum < 1, $"expected no data rows but LastRowNum was {sheet.LastRowNum}");
        }

        // ===== BuildTaskFailureDetail =====

        [TestMethod]
        public void BuildTaskFailureDetail_IncludesCountsAndMessage()
        {
            var result = new DataImportConsumer.ImportRunResult
            {
                ProcessedCount = 100,
                AddCount = 80,
                UpdateCount = 10
            };
            var ex = new InvalidOperationException("读取失败");

            var detail = DataImportConsumer.BuildTaskFailureDetail(result, ex);

            StringAssert.Contains(detail, "100");
            StringAssert.Contains(detail, "80");
            StringAssert.Contains(detail, "10");
            StringAssert.Contains(detail, "读取失败");
        }

        // ===== helpers =====

        private static ExpandoObject NewExpando(IDictionary<string, object?> dict)
        {
            var exp = new ExpandoObject();
            var d = (IDictionary<string, object?>)exp;
            foreach (var kv in dict) d[kv.Key] = kv.Value;
            return exp;
        }

        private static string GetCellText(ICell? cell)
        {
            if (cell == null) return string.Empty;
            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue ?? string.Empty,
                CellType.Numeric => cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellType.Boolean => cell.BooleanCellValue ? "true" : "false",
                _ => cell.ToString() ?? string.Empty
            };
        }
    }
}
