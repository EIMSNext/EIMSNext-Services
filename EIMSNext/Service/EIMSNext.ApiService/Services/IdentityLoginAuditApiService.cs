using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Entities;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 登录审计的 API 服务。
    /// </summary>
    /// <param name="resolver">服务解析器。</param>
    public class IdentityLoginAuditApiService(IResolver resolver) : ApiServiceBase<IdentityLoginAudit, IdentityLoginAuditViewModel, IIdentityLoginAuditService>(resolver)
    {
        private static readonly Dictionary<string, ExportColumnType> IdentityLoginAuditColumnTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["userName"] = ExportColumnType.String,
            ["createTime"] = ExportColumnType.Date,
            ["loginId"] = ExportColumnType.String,
            ["failReason"] = ExportColumnType.String,
            ["clientIp"] = ExportColumnType.String,
        };

        /// <summary>
        /// 执行 ExportAsync 操作。
        /// </summary>
        public async Task<ExportResponse> ExportAsync(IdentityLoginAuditExportRequest request)
        {
            ValidateLoginExportRequest(request);

            var totalCount = await CountExportAsync(request);
            var actualFormat = totalCount > 100000 ? ExportFormat.Csv : request.Format;
            var createBy = IdentityContext.CurrentEmployee?.Id ?? string.Empty;
            var columnsJson = request.Columns.SerializeToJson();
            var filterJson = request.SerializeToJson();
            var dedupKey = BuildDedupKey(new
            {
                ExportType = ExportType.IdentityLoginAudit,
                request.Format,
                request.Columns,
                request.UserName,
                request.StartTime,
                request.EndTime,
            });

            var exportLogService = Resolver.Resolve<IExportLogService>();
            var duplicated = await exportLogService.GetDuplicatedPendingAsync(IdentityContext.CurrentCorpId, createBy, dedupKey);
            if (duplicated != null)
            {
                return new ExportResponse
                {
                    TaskId = duplicated.Id,
                    IsDuplicate = true,
                    ActualFormat = duplicated.ActualFormat,
                    Message = "已有相同条件的导出任务正在处理中",
                };
            }

            var exportLog = new ExportLog
            {
                CorpId = IdentityContext.CurrentCorpId,
                ExportType = ExportType.IdentityLoginAudit,
                RequestedFormat = request.Format,
                ActualFormat = actualFormat,
                Status = ExportLogStatus.Pending,
                ColumnsJson = columnsJson,
                FilterJson = filterJson,
                DedupKey = dedupKey,
                TotalCount = totalCount,
            };

            await exportLogService.AddAsync(exportLog);
            await Resolver.Resolve<IMessagePublisher>().PublishAsync(new DataExportTaskArgs
            {
                ExportLogId = exportLog.Id,
                CorpId = exportLog.CorpId ?? string.Empty,
            });

            return new ExportResponse
            {
                TaskId = exportLog.Id,
                IsDuplicate = false,
                ActualFormat = actualFormat,
                Message = actualFormat != request.Format ? "超过 10W 行，已自动切换为 CSV 导出" : null,
            };
        }

        private async Task<long> CountExportAsync(IdentityLoginAuditExportRequest request)
        {
            var filter = BuildIdentityLoginAuditFilter(request);
            return await Resolver.GetRepository<IdentityLoginAudit>().Find(filter).LongCountAsync();
        }

        private Expression<Func<IdentityLoginAudit, bool>> BuildIdentityLoginAuditFilter(IdentityLoginAuditExportRequest request)
        {
            var corpId = IdentityContext.CurrentCorpId;
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

        private static void ValidateLoginExportRequest(IdentityLoginAuditExportRequest request)
        {
            if (request.Columns == null || request.Columns.Count == 0)
            {
                throw new ArgumentException("导出列不能为空");
            }

            foreach (var column in request.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Key))
                {
                    throw new ArgumentException("导出列标识不能为空");
                }

                if (string.IsNullOrWhiteSpace(column.Header))
                {
                    throw new ArgumentException($"导出列标题不能为空: {column.Key}");
                }

                if (!IdentityLoginAuditColumnTypes.TryGetValue(column.Key, out var type))
                {
                    throw new ArgumentException($"不支持的导出列: {column.Key}");
                }

                column.Type = type;
            }

            request.Columns = request.Columns
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();
        }

        private static string BuildDedupKey(object source)
        {
            var json = source.SerializeToJson();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes);
        }
    }
}
