using System.Linq.Expressions;
using System.Composition;
using System.Text.Json;

using EIMSNext.ApiService.RequestModels;
using EIMSNext.Entities;
using EIMSNext.Mef;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Async.Tasks.Export
{
    [Export(typeof(IExportProcessor))]
    [ExportMetadata(MefMetadata.Id, ExportProcessorIds.IdentityLoginAudit)]
    public class IdentityLoginAuditExportProcessor : ExportProcessorBase
    {
        public override async Task<ExportFileBuilder.ExportFileResult> ExportAsync(
            ExportLog exportLog,
            IResolver resolver,
            CancellationToken ct)
        {
            var columns = exportLog.ColumnsJson?.DeserializeFromJson<List<ExportColumn>>() ?? [];
            var request = exportLog.FilterJson?.DeserializeFromJson<IdentityLoginAuditExportRequest>() ?? new IdentityLoginAuditExportRequest();
            var filter = BuildFilter(exportLog.CorpId ?? string.Empty, request);

            var result = await (exportLog.ActualFormat == ExportFormat.Excel
                ? ExportExcelByBatchAsync<IdentityLoginAudit>(
                    $"login-log-{Guid.NewGuid():N}.xlsx",
                    columns,
                    filter,
                    resolver,
                    ct,
                    2000,
                    WriteExcelRows)
                : ExportCsvByBatchAsync<IdentityLoginAudit>(
                    $"login-log-{Guid.NewGuid():N}.csv",
                    columns,
                    filter,
                    resolver,
                    ct,
                    2000,
                    WriteCsvRows));
            result.FormName = "登录日志";
            return result;
        }

        internal static ExportFileBuilder.ExportCellValue GetCellValue(ExportColumn column, IdentityLoginAudit row)
        {
            return column.Key switch
            {
                "userName" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.UserName ?? row.CreateBy?.Label ?? "-") },
                "createTime" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.Date, DateTime = ExportFileBuilder.ToLocalDateTime(row.CreateTime) },
                "loginId" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.LoginId ?? "-") },
                "failReason" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.FailReason ?? "-") },
                "clientIp" => new ExportFileBuilder.ExportCellValue { Type = ExportColumnType.String, Text = ExportFileBuilder.SanitizeForExcel(row.ClientIp ?? "-") },
                _ => new ExportFileBuilder.ExportCellValue(),
            };
        }

        internal static void WriteCsvRows(HKH.CSV.CSVWriter writer, List<ExportColumn> columns, IEnumerable<IdentityLoginAudit> rows)
        {
            foreach (var row in rows)
            {
                writer.Write(columns.Select(x => ExportFileBuilder.FormatCsvCell(GetCellValue(x, row))), false);
            }
        }

        internal static int WriteExcelRows(NPOI.SS.UserModel.ISheet sheet, ExportFileBuilder.ExcelStyles styles, List<ExportColumn> columns, IEnumerable<IdentityLoginAudit> rows, int startRowIndex)
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

        private static Expression<Func<IdentityLoginAudit, bool>> BuildFilter(string corpId, IdentityLoginAuditExportRequest request)
        {
            Expression<Func<IdentityLoginAudit, bool>> filter = x => x.CorpId == corpId && !x.DeleteFlag;

            if (!string.IsNullOrWhiteSpace(request.UserName))
            {
                var keyword = DynamicQueryExtensions.EscapeLikePattern(request.UserName);
                filter = filter.AndAlso(x => EF.Functions.ILike(x.UserName, keyword));
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
