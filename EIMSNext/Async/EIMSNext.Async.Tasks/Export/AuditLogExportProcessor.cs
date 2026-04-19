using System.Composition;
using System.Text.Json;

using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using MongoDB.Driver;

namespace EIMSNext.Async.Tasks.Export
{
    [Export(typeof(IExportProcessor))]
    [ExportMetadata(MefMetadata.Id, ExportProcessorIds.AuditLog)]
    public class AuditLogExportProcessor : ExportProcessorBase
    {
        public override async Task<ExportFileBuilder.ExportFileResult> ExportAsync(
            ExportLog exportLog,
            IResolver resolver,
            CancellationToken ct)
        {
            var columns = exportLog.ColumnsJson?.DeserializeFromJson<List<ExportColumn>>() ?? [];
            var request = exportLog.FilterJson?.DeserializeFromJson<AuditLogExportRequest>() ?? new AuditLogExportRequest();
            var filter = BuildFilter(exportLog.CorpId ?? string.Empty, request);

            var result = await (exportLog.ActualFormat == ExportFormat.Excel
                ? ExportExcelByBatchAsync<AuditLog>(
                    $"action-log-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx",
                    "操作日志",
                    columns,
                    filter,
                    resolver,
                    ct,
                    2000,
                    WriteExcelRows)
                : ExportCsvByBatchAsync<AuditLog>(
                    $"action-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                    columns,
                    filter,
                    resolver,
                    ct,
                    2000,
                    WriteCsvRows));
            result.FormName = "操作日志";
            return result;
        }

        internal static ExportFileBuilder.ExportCellValue GetCellValue(ExportColumn column, AuditLog row)
        {
            return column.Key switch
            {
                "operatorName" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.CreateBy?.Label ?? "-") },
                "createTime" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.Date, DateTime = ExportFileBuilder.ToLocalDateTime(row.CreateTime) },
                "action" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.Action.ToString()) },
                "entityType" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.EntityType ?? "-") },
                "detail" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.Detail ?? "-") },
                "clientIp" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.ClientIp ?? "-") },
                _ => new ExportFileBuilder.ExportCellValue(),
            };
        }

        internal static void WriteCsvRows(HKH.CSV.CSVWriter writer, List<ExportColumn> columns, IEnumerable<AuditLog> rows)
        {
            foreach (var row in rows)
            {
                writer.Write(columns.Select(x => ExportFileBuilder.FormatCsvCell(GetCellValue(x, row))), false);
            }
        }

        internal static int WriteExcelRows(NPOI.SS.UserModel.ISheet sheet, ExportFileBuilder.ExcelStyles styles, List<ExportColumn> columns, IEnumerable<AuditLog> rows, int startRowIndex)
        {
            var rowIndex = startRowIndex;
            foreach (var item in rows)
            {
                var row = sheet.CreateRow(rowIndex++);
                for (var colIndex = 0; colIndex < columns.Count; colIndex++)
                {
                    ExportFileBuilder.WriteExcelCell(row.CreateCell(colIndex), GetCellValue(columns[colIndex], item), styles);
                }
            }

            return rowIndex;
        }

        private static FilterDefinition<AuditLog> BuildFilter(string corpId, AuditLogExportRequest request)
        {
            var builder = Builders<AuditLog>.Filter;
            var filters = new List<FilterDefinition<AuditLog>>
            {
                builder.Eq(x => x.CorpId, corpId),
                builder.Ne(x => x.DeleteFlag, true),
            };

            if (!string.IsNullOrWhiteSpace(request.EntityType))
            {
                filters.Add(builder.Eq(x => x.EntityType, request.EntityType));
            }

            if (!string.IsNullOrWhiteSpace(request.Action) && Enum.TryParse<DbAction>(request.Action, true, out var action))
            {
                filters.Add(builder.Eq(x => x.Action, action));
            }

            if (!string.IsNullOrWhiteSpace(request.OperatorName))
            {
                filters.Add(builder.Regex("CreateBy.Label", new MongoDB.Bson.BsonRegularExpression(request.OperatorName, "i")));
            }

            if (request.StartTime.HasValue)
            {
                filters.Add(builder.Gte(x => x.CreateTime, request.StartTime.Value));
            }

            if (request.EndTime.HasValue)
            {
                filters.Add(builder.Lte(x => x.CreateTime, request.EndTime.Value));
            }

            return filters.Count == 1 ? filters[0] : builder.And(filters);
        }
    }
}
