using System.Linq.Expressions;
using System.Composition;
using System.Text.Json;

using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Mef;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;

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
                    $"action-log-{Guid.NewGuid():N}.xlsx",
                    columns,
                    filter,
                    resolver,
                    ct,
                    2000,
                    WriteExcelRows)
                : ExportCsvByBatchAsync<AuditLog>(
                    $"action-log-{Guid.NewGuid():N}.csv",
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

        private static Expression<Func<AuditLog, bool>> BuildFilter(string corpId, AuditLogExportRequest request)
        {
            Expression<Func<AuditLog, bool>> filter = x => x.CorpId == corpId && !x.DeleteFlag;

            if (!string.IsNullOrWhiteSpace(request.EntityType))
            {
                var entityType = request.EntityType;
                filter = filter.AndAlso(x => x.EntityType == entityType);
            }

            if (!string.IsNullOrWhiteSpace(request.Action) && Enum.TryParse<DbAction>(request.Action, true, out var action))
            {
                filter = filter.AndAlso(x => x.Action == action);
            }

            if (!string.IsNullOrWhiteSpace(request.OperatorName))
            {
                var keyword = DynamicQueryExtensions.EscapeLikePattern(request.OperatorName);
                filter = filter.AndAlso(x => x.CreateBy != null && EF.Functions.ILike(x.CreateBy.Label, keyword));
            }

            if (request.StartTime.HasValue)
            {
                var startTime = request.StartTime.Value;
                filter = filter.AndAlso(x => x.CreateTime >= startTime);
            }

            if (request.EndTime.HasValue)
            {
                var endTime = request.EndTime.Value;
                filter = filter.AndAlso(x => x.CreateTime <= endTime);
            }

            return filter;
        }
    }
}
