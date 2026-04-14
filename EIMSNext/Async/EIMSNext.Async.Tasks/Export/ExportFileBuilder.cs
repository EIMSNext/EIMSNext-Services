using System.Text;
using EIMSNext.Auth.Entities;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.RequestModels;
using HKH.CSV;
using NPOI.SS.UserModel;
using NPOI.XSSF.Streaming;

namespace EIMSNext.Async.Tasks.Export
{
    public static class ExportFileBuilder
    {
        public sealed class ExportFileResult
        {
            public string FileName { get; init; } = string.Empty;

            public byte[] Content { get; init; } = [];

            public long TotalCount { get; init; }
        }

        public sealed class ExportCellValue
        {
            public string Text { get; init; } = string.Empty;

            public decimal? Number { get; init; }

            public DateTime? DateTime { get; init; }

            public ExportColumnType Type { get; init; } = ExportColumnType.String;
        }

        public sealed class ExcelStyles
        {
            public required ICellStyle Header { get; init; }

            public required ICellStyle Text { get; init; }

            public required ICellStyle Number { get; init; }

            public required ICellStyle Date { get; init; }
        }

        public static ExportFileResult BuildAuditLogin(ExportFormat format, List<ExportColumn> columns, List<AuditLogin> rows)
        {
            return new ExportFileResult
            {
                FileName = $"login-log-{DateTime.Now:yyyyMMdd-HHmmss}.{GetFileExtension(format)}",
                Content = format == ExportFormat.Excel
                    ? BuildAuditLoginExcel(columns, rows)
                    : BuildAuditLoginCsv(columns, rows),
                TotalCount = rows.Count,
            };
        }

        public static ExportFileResult BuildAuditLog(ExportFormat format, List<ExportColumn> columns, List<AuditLog> rows)
        {
            return new ExportFileResult
            {
                FileName = $"action-log-{DateTime.Now:yyyyMMdd-HHmmss}.{GetFileExtension(format)}",
                Content = format == ExportFormat.Excel
                    ? BuildAuditLogExcel(columns, rows)
                    : BuildAuditLogCsv(columns, rows),
                TotalCount = rows.Count,
            };
        }

        public static byte[] BuildAuditLoginCsv(List<ExportColumn> columns, List<AuditLogin> rows)
        {
            using var ms = new MemoryStream();
            using var writer = CreateCsvWriter(ms);
            WriteCsvHeader(writer, columns);
            WriteAuditLoginCsvRows(writer, columns, rows);
            writer.Flush();
            return ms.ToArray();
        }

        public static byte[] BuildAuditLogCsv(List<ExportColumn> columns, List<AuditLog> rows)
        {
            using var ms = new MemoryStream();
            using var writer = CreateCsvWriter(ms);
            WriteCsvHeader(writer, columns);
            WriteAuditLogCsvRows(writer, columns, rows);
            writer.Flush();
            return ms.ToArray();
        }

        public static byte[] BuildAuditLoginExcel(List<ExportColumn> columns, List<AuditLogin> rows)
        {
            using var workbook = new SXSSFWorkbook(100);
            var sheet = InitializeExcelSheet(workbook, "登录日志", columns, out var styles);
            WriteAuditLoginExcelRows(sheet, styles, columns, rows, 1);

            using var ms = new MemoryStream();
            workbook.Write(ms, false);
            workbook.Dispose();
            return ms.ToArray();
        }

        public static byte[] BuildAuditLogExcel(List<ExportColumn> columns, List<AuditLog> rows)
        {
            using var workbook = new SXSSFWorkbook(100);
            var sheet = InitializeExcelSheet(workbook, "操作日志", columns, out var styles);
            WriteAuditLogExcelRows(sheet, styles, columns, rows, 1);

            using var ms = new MemoryStream();
            workbook.Write(ms, false);
            workbook.Dispose();
            return ms.ToArray();
        }

        public static ExportCellValue GetAuditLoginCellValue(ExportColumn column, AuditLogin row)
        {
            return column.Key switch
            {
                "userName" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.UserName ?? row.CreateBy?.Label ?? "-") },
                "createTime" => new ExportCellValue { Type = ExportColumnType.Date, DateTime = ToLocalDateTime(row.CreateTime) },
                "loginId" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.LoginId ?? "-") },
                "failReason" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.FailReason ?? "-") },
                "clientIp" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.ClientIp ?? "-") },
                _ => new ExportCellValue(),
            };
        }

        public static ExportCellValue GetAuditLogCellValue(ExportColumn column, AuditLog row)
        {
            return column.Key switch
            {
                "operatorName" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.CreateBy?.Label ?? "-") },
                "createTime" => new ExportCellValue { Type = ExportColumnType.Date, DateTime = ToLocalDateTime(row.CreateTime) },
                "action" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.Action.ToString()) },
                "entityType" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.EntityType ?? "-") },
                "detail" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.Detail ?? "-") },
                "clientIp" => new ExportCellValue { Type = ExportColumnType.String, Text = SanitizeForExcel(row.ClientIp ?? "-") },
                _ => new ExportCellValue(),
            };
        }

        public static CSVWriter CreateCsvWriter(Stream stream)
        {
            return new CSVWriter(stream, ',', '"', '"', "\r\n");
        }

        public static void WriteCsvHeader(CSVWriter writer, List<ExportColumn> columns)
        {
            writer.Write(columns.Select(x => x.Header), false);
        }

        public static void WriteAuditLoginCsvRows(CSVWriter writer, List<ExportColumn> columns, IEnumerable<AuditLogin> rows)
        {
            foreach (var row in rows)
            {
                writer.Write(columns.Select(x => FormatCsvCell(GetAuditLoginCellValue(x, row))), false);
            }
        }

        public static void WriteAuditLogCsvRows(CSVWriter writer, List<ExportColumn> columns, IEnumerable<AuditLog> rows)
        {
            foreach (var row in rows)
            {
                writer.Write(columns.Select(x => FormatCsvCell(GetAuditLogCellValue(x, row))), false);
            }
        }

        public static byte[] WriteCsv(List<List<string>> rows)
        {
            using var ms = new MemoryStream();
            using var writer = CreateCsvWriter(ms);
            writer.Write(rows, false);
            writer.Flush();
            return ms.ToArray();
        }

        public static string FormatCsvCell(ExportCellValue cell)
        {
            return cell.Type switch
            {
                ExportColumnType.Number => cell.Number?.ToString("0.00") ?? string.Empty,
                ExportColumnType.Date => cell.DateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                _ => cell.Text ?? string.Empty,
            };
        }

        public static void SetColumnWidths(ISheet sheet, List<ExportColumn> columns)
        {
            foreach (var (column, index) in columns.Select((x, i) => (x, i)))
            {
                var width = column.Key switch
                {
                    "createTime" => 22,
                    "userName" => 18,
                    "operatorName" => 18,
                    "loginId" => 20,
                    "clientIp" => 16,
                    "action" => 16,
                    "entityType" => 18,
                    "failReason" => 24,
                    "detail" => 50,
                    _ => 18,
                };

                sheet.SetColumnWidth(index, width * 256);
            }
        }

        public static SXSSFWorkbook CreateWorkbook()
        {
            return new SXSSFWorkbook(100);
        }

        public static ISheet InitializeExcelSheet(IWorkbook workbook, string sheetName, List<ExportColumn> columns, out ExcelStyles styles)
        {
            var sheet = workbook.CreateSheet(sheetName);
            sheet.CreateFreezePane(0, 1);
            SetColumnWidths(sheet, columns);

            styles = CreateExcelStyles(workbook);
            var headerRow = sheet.CreateRow(0);
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(columns[i].Header);
                cell.CellStyle = styles.Header;
            }

            return sheet;
        }

        public static ExcelStyles CreateExcelStyles(IWorkbook workbook)
        {
            var dataFormat = workbook.CreateDataFormat();

            var header = workbook.CreateCellStyle();
            header.Alignment = HorizontalAlignment.Center;
            header.VerticalAlignment = VerticalAlignment.Center;
            header.BorderBottom = BorderStyle.Thin;
            header.BorderTop = BorderStyle.Thin;
            header.BorderLeft = BorderStyle.Thin;
            header.BorderRight = BorderStyle.Thin;

            var headerFont = workbook.CreateFont();
            headerFont.IsBold = true;
            header.SetFont(headerFont);

            var text = workbook.CreateCellStyle();
            text.Alignment = HorizontalAlignment.Left;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.BorderBottom = BorderStyle.Thin;
            text.BorderTop = BorderStyle.Thin;
            text.BorderLeft = BorderStyle.Thin;
            text.BorderRight = BorderStyle.Thin;
            text.WrapText = true;

            var number = workbook.CreateCellStyle();
            number.CloneStyleFrom(text);
            number.DataFormat = dataFormat.GetFormat("0.00");
            number.Alignment = HorizontalAlignment.Right;

            var date = workbook.CreateCellStyle();
            date.CloneStyleFrom(text);
            date.DataFormat = dataFormat.GetFormat("yyyy-MM-dd HH:mm:ss");

            return new ExcelStyles
            {
                Header = header,
                Text = text,
                Number = number,
                Date = date,
            };
        }

        public static void WriteExcelCell(ICell cell, ExportCellValue value, ExcelStyles styles)
        {
            switch (value.Type)
            {
                case ExportColumnType.Number:
                    if (value.Number.HasValue)
                    {
                        cell.SetCellValue(Convert.ToDouble(value.Number.Value));
                    }
                    else
                    {
                        cell.SetCellValue(string.Empty);
                    }
                    cell.CellStyle = styles.Number;
                    break;
                case ExportColumnType.Date:
                    if (value.DateTime.HasValue)
                    {
                        cell.SetCellValue(value.DateTime.Value);
                    }
                    else
                    {
                        cell.SetCellValue(string.Empty);
                    }
                    cell.CellStyle = styles.Date;
                    break;
                default:
                    cell.SetCellValue(value.Text ?? string.Empty);
                    cell.CellStyle = styles.Text;
                    break;
            }
        }

        public static int WriteAuditLoginExcelRows(ISheet sheet, ExcelStyles styles, List<ExportColumn> columns, IEnumerable<AuditLogin> rows, int startRowIndex)
        {
            var rowIndex = startRowIndex;
            foreach (var item in rows)
            {
                var row = sheet.CreateRow(rowIndex++);
                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    WriteExcelCell(row.CreateCell(colIndex), GetAuditLoginCellValue(columns[colIndex], item), styles);
                }
            }

            return rowIndex;
        }

        public static int WriteAuditLogExcelRows(ISheet sheet, ExcelStyles styles, List<ExportColumn> columns, IEnumerable<AuditLog> rows, int startRowIndex)
        {
            var rowIndex = startRowIndex;
            foreach (var item in rows)
            {
                var row = sheet.CreateRow(rowIndex++);
                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    WriteExcelCell(row.CreateCell(colIndex), GetAuditLogCellValue(columns[colIndex], item), styles);
                }
            }

            return rowIndex;
        }

        public static DateTime? ToLocalDateTime(long? timestamp)
        {
            if (!timestamp.HasValue || timestamp.Value <= 0)
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).ToLocalTime().DateTime;
        }

        public static string SanitizeForExcel(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value[0] is '=' or '+' or '-' or '@' ? $"'{value}" : value;
        }

        public static string GetFileExtension(ExportFormat format)
        {
            return format == ExportFormat.Excel ? "xlsx" : "csv";
        }
    }
}
