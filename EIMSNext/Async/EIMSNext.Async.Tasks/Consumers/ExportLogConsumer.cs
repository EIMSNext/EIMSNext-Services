using System.Text.Json;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Tasks.Export;
using EIMSNext.Auth.Entities;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Storage.Abstractions;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EIMSNext.Async.Tasks.Consumers
{
    public class ExportLogConsumer : TaskConsumerBase<ExportLogTaskArgs, ExportLogConsumer>
    {
        public ExportLogConsumer(IServiceScopeFactory scopeFactory)
            : base(scopeFactory)
        {
        }

        protected override async Task HandleAsync(ExportLogTaskArgs args, CancellationToken ct, IResolver resolver)
        {
            var exportLogService = resolver.Resolve<IExportLogService>();
            var exportLog = exportLogService.Get(args.ExportLogId);
            if (exportLog == null || exportLog.Status == ExportLogStatus.Succeeded)
            {
                return;
            }

            await exportLogService.MarkProcessingAsync(exportLog.Id);

            try
            {
                var result = exportLog.ExportType switch
                {
                    ExportType.AuditLogin => await ExportAuditLoginAsync(exportLog, resolver, ct),
                    ExportType.AuditLog => await ExportAuditLogAsync(exportLog, resolver, ct),
                    _ => throw new NotSupportedException($"Unsupported export type: {exportLog.ExportType}"),
                };

                var savePath = $"Export\\{exportLog.CorpId}\\Logs\\{DateTime.UtcNow:yyyyMMdd}\\{result.FileName}";
                var storage = resolver.Resolve<IStorageProvider>();
                if (!storage.Upload(result.Content, savePath))
                {
                    throw new InvalidOperationException("上传导出文件失败");
                }

                var downloadUrl = $"{storage.Setting.BaseUrl.TrimEnd('/')}/{savePath.TrimStart('/', '\\').Replace("\\", "/")}";
                await exportLogService.MarkSucceededAsync(exportLog.Id, result.FileName, downloadUrl, result.TotalCount, exportLog.ActualFormat);
                await PublishSuccessMessageAsync(exportLog, result.TotalCount, downloadUrl, resolver, ct);
            }
            catch (Exception ex)
            {
                await exportLogService.MarkFailedAsync(exportLog.Id, ex.Message);
                await PublishFailedMessageAsync(exportLog, ex.Message, resolver, ct);
            }
        }

        private static async Task<ExportFileBuilder.ExportFileResult> ExportAuditLoginAsync(ExportLog exportLog, IResolver resolver, CancellationToken ct)
        {
            var columns = exportLog.ColumnsJson?.DeserializeFromJson<List<ExportColumn>>() ?? [];
            var request = exportLog.FilterJson?.DeserializeFromJson<AuditLoginExportRequest>() ?? new AuditLoginExportRequest();
            var filter = BuildAuditLoginFilter(exportLog.CorpId ?? string.Empty, request);

            return exportLog.ActualFormat == ExportFormat.Excel
                ? await ExportAuditLoginExcelAsync(columns, filter, resolver, ct)
                : await ExportAuditLoginCsvAsync(columns, filter, resolver, ct);
        }

        private static async Task<ExportFileBuilder.ExportFileResult> ExportAuditLogAsync(ExportLog exportLog, IResolver resolver, CancellationToken ct)
        {
            var columns = exportLog.ColumnsJson?.DeserializeFromJson<List<ExportColumn>>() ?? [];
            var request = exportLog.FilterJson?.DeserializeFromJson<AuditLogExportRequest>() ?? new AuditLogExportRequest();
            var filter = BuildAuditLogFilter(exportLog.CorpId ?? string.Empty, request);

            return exportLog.ActualFormat == ExportFormat.Excel
                ? await ExportAuditLogExcelAsync(columns, filter, resolver, ct)
                : await ExportAuditLogCsvAsync(columns, filter, resolver, ct);
        }

        private static async Task<ExportFileBuilder.ExportFileResult> ExportAuditLoginCsvAsync(List<ExportColumn> columns, FilterDefinition<AuditLogin> filter, IResolver resolver, CancellationToken ct)
        {
            var fileName = $"login-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var tempFile = Path.GetTempFileName();
            long totalCount = 0;
            const int batchSize = 2000;

            try
            {
                await using var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = ExportFileBuilder.CreateCsvWriter(stream);
                ExportFileBuilder.WriteCsvHeader(writer, columns);

                var repo = resolver.GetRepository<AuditLogin>();
                long? lastCreateTime = null;
                string? lastId = null;
                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, repo.FilterBuilder, lastCreateTime, lastId);
                    var rows = await repo.Find(new MongoFindOptions<AuditLogin>
                    {
                        Filter = batchFilter,
                        Sort = repo.SortBuilder.Descending(x => x.CreateTime).Descending(x => x.Id),
                        Take = batchSize,
                    }).ToListAsync(ct);

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    ExportFileBuilder.WriteAuditLoginCsvRows(writer, columns, rows);
                    writer.Flush();
                    totalCount += rows.Count;

                    var last = rows[^1];
                    lastCreateTime = last.CreateTime;
                    lastId = last.Id;
                }

                await stream.FlushAsync(ct);
                return new ExportFileBuilder.ExportFileResult
                {
                    FileName = fileName,
                    Content = await File.ReadAllBytesAsync(tempFile, ct),
                    TotalCount = totalCount,
                };
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static async Task<ExportFileBuilder.ExportFileResult> ExportAuditLogCsvAsync(List<ExportColumn> columns, FilterDefinition<AuditLog> filter, IResolver resolver, CancellationToken ct)
        {
            var fileName = $"action-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var tempFile = Path.GetTempFileName();
            long totalCount = 0;
            const int batchSize = 2000;

            try
            {
                await using var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = ExportFileBuilder.CreateCsvWriter(stream);
                ExportFileBuilder.WriteCsvHeader(writer, columns);

                var repo = resolver.GetRepository<AuditLog>();
                long? lastCreateTime = null;
                string? lastId = null;
                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, repo.FilterBuilder, lastCreateTime, lastId);
                    var rows = await repo.Find(new MongoFindOptions<AuditLog>
                    {
                        Filter = batchFilter,
                        Sort = repo.SortBuilder.Descending(x => x.CreateTime).Descending(x => x.Id),
                        Take = batchSize,
                    }).ToListAsync(ct);

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    ExportFileBuilder.WriteAuditLogCsvRows(writer, columns, rows);
                    writer.Flush();
                    totalCount += rows.Count;

                    var last = rows[^1];
                    lastCreateTime = last.CreateTime;
                    lastId = last.Id;
                }

                await stream.FlushAsync(ct);
                return new ExportFileBuilder.ExportFileResult
                {
                    FileName = fileName,
                    Content = await File.ReadAllBytesAsync(tempFile, ct),
                    TotalCount = totalCount,
                };
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static async Task<ExportFileBuilder.ExportFileResult> ExportAuditLoginExcelAsync(List<ExportColumn> columns, FilterDefinition<AuditLogin> filter, IResolver resolver, CancellationToken ct)
        {
            var fileName = $"login-log-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            var tempFile = Path.GetTempFileName();
            long totalCount = 0;
            const int batchSize = 2000;

            try
            {
                using var workbook = ExportFileBuilder.CreateWorkbook();
                var sheet = ExportFileBuilder.InitializeExcelSheet(workbook, "登录日志", columns, out var styles);
                var repo = resolver.GetRepository<AuditLogin>();
                var rowIndex = 1;
                long? lastCreateTime = null;
                string? lastId = null;

                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, repo.FilterBuilder, lastCreateTime, lastId);
                    var rows = await repo.Find(new MongoFindOptions<AuditLogin>
                    {
                        Filter = batchFilter,
                        Sort = repo.SortBuilder.Descending(x => x.CreateTime).Descending(x => x.Id),
                        Take = batchSize,
                    }).ToListAsync(ct);

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    rowIndex = ExportFileBuilder.WriteAuditLoginExcelRows(sheet, styles, columns, rows, rowIndex);
                    totalCount += rows.Count;

                    var last = rows[^1];
                    lastCreateTime = last.CreateTime;
                    lastId = last.Id;
                }

                await using var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                workbook.Write(stream, false);
                workbook.Dispose();
                await stream.FlushAsync(ct);

                return new ExportFileBuilder.ExportFileResult
                {
                    FileName = fileName,
                    Content = await File.ReadAllBytesAsync(tempFile, ct),
                    TotalCount = totalCount,
                };
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static async Task<ExportFileBuilder.ExportFileResult> ExportAuditLogExcelAsync(List<ExportColumn> columns, FilterDefinition<AuditLog> filter, IResolver resolver, CancellationToken ct)
        {
            var fileName = $"action-log-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            var tempFile = Path.GetTempFileName();
            long totalCount = 0;
            const int batchSize = 2000;

            try
            {
                using var workbook = ExportFileBuilder.CreateWorkbook();
                var sheet = ExportFileBuilder.InitializeExcelSheet(workbook, "操作日志", columns, out var styles);
                var repo = resolver.GetRepository<AuditLog>();
                var rowIndex = 1;
                long? lastCreateTime = null;
                string? lastId = null;

                while (true)
                {
                    var batchFilter = BuildSeekFilter(filter, repo.FilterBuilder, lastCreateTime, lastId);
                    var rows = await repo.Find(new MongoFindOptions<AuditLog>
                    {
                        Filter = batchFilter,
                        Sort = repo.SortBuilder.Descending(x => x.CreateTime).Descending(x => x.Id),
                        Take = batchSize,
                    }).ToListAsync(ct);

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    rowIndex = ExportFileBuilder.WriteAuditLogExcelRows(sheet, styles, columns, rows, rowIndex);
                    totalCount += rows.Count;

                    var last = rows[^1];
                    lastCreateTime = last.CreateTime;
                    lastId = last.Id;
                }

                await using var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                workbook.Write(stream, false);
                workbook.Dispose();
                await stream.FlushAsync(ct);

                return new ExportFileBuilder.ExportFileResult
                {
                    FileName = fileName,
                    Content = await File.ReadAllBytesAsync(tempFile, ct),
                    TotalCount = totalCount,
                };
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static FilterDefinition<AuditLogin> BuildAuditLoginFilter(string corpId, AuditLoginExportRequest request)
        {
            var builder = Builders<AuditLogin>.Filter;
            var filters = new List<FilterDefinition<AuditLogin>>
            {
                builder.Eq(x => x.CorpId, corpId),
                builder.Ne(x => x.DeleteFlag, true),
            };

            if (!string.IsNullOrWhiteSpace(request.UserName))
            {
                filters.Add(builder.Regex(x => x.UserName, new MongoDB.Bson.BsonRegularExpression(request.UserName, "i")));
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

        private static FilterDefinition<AuditLog> BuildAuditLogFilter(string corpId, AuditLogExportRequest request)
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

        private static async Task PublishSuccessMessageAsync(ExportLog exportLog, long totalCount, string downloadUrl, IResolver resolver, CancellationToken ct)
        {
            var employee = ResolveOwner(resolver, exportLog);
            if (employee == null)
            {
                return;
            }

            await resolver.Resolve<IMessagePublisher>().PublishAsync(new SystemMessageTaskArgs
            {
                CorpId = exportLog.CorpId ?? string.Empty,
                NotifyId = exportLog.Id,
                Title = "日志导出已完成",
                Detail = $"{GetExportTypeName(exportLog.ExportType)}导出完成，共 {totalCount} 条",
                Url = downloadUrl,
                ExpireTime = DateTime.UtcNow.AddDays(30).ToTimeStampMs(),
                Category = MessageCategory.SystemNotify,
                MessageType = MessageType.FormNotify,
                Receivers =
                [
                    new NotifyReceiver
                    {
                        EmpId = employee.Id,
                        EmpName = employee.EmpName,
                        Email = employee.WorkEmail,
                    }
                ]
            }, ct);
        }

        private static async Task PublishFailedMessageAsync(ExportLog exportLog, string errorMessage, IResolver resolver, CancellationToken ct)
        {
            var employee = ResolveOwner(resolver, exportLog);
            if (employee == null)
            {
                return;
            }

            await resolver.Resolve<IMessagePublisher>().PublishAsync(new SystemMessageTaskArgs
            {
                CorpId = exportLog.CorpId ?? string.Empty,
                NotifyId = exportLog.Id,
                Title = "日志导出失败",
                Detail = errorMessage,
                Url = string.Empty,
                ExpireTime = DateTime.UtcNow.AddDays(30).ToTimeStampMs(),
                Category = MessageCategory.SystemNotify,
                MessageType = MessageType.FormNotify,
                Receivers =
                [
                    new NotifyReceiver
                    {
                        EmpId = employee.Id,
                        EmpName = employee.EmpName,
                        Email = employee.WorkEmail,
                    }
                ]
            }, ct);
        }

        private static Employee? ResolveOwner(IResolver resolver, ExportLog exportLog)
        {
            var empId = exportLog.CreateBy?.Value;
            if (string.IsNullOrWhiteSpace(empId))
            {
                return null;
            }

            return resolver.GetRepository<Employee>().Get(empId);
        }

        private static string GetExportTypeName(ExportType exportType)
        {
            return exportType switch
            {
                ExportType.AuditLogin => "登录日志",
                ExportType.AuditLog => "操作日志",
                _ => "日志",
            };
        }

        internal static FilterDefinition<T> BuildSeekFilter<T>(FilterDefinition<T> baseFilter, FilterDefinitionBuilder<T> builder, long? lastCreateTime, string? lastId)
            where T : Core.Entities.EntityBase
        {
            if (!lastCreateTime.HasValue || string.IsNullOrWhiteSpace(lastId))
            {
                return baseFilter;
            }

            var seekFilter = builder.Or(
                builder.Lt(x => x.CreateTime, lastCreateTime.Value),
                builder.And(
                    builder.Eq(x => x.CreateTime, lastCreateTime.Value),
                    builder.Lt(x => x.Id, lastId)));

            return builder.And(baseFilter, seekFilter);
        }
    }
}
