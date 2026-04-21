using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;
using EIMSNext.Async.Tasks.Export;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using EIMSNext.Storage.Abstractions;
using HKH.Mef2.Integration;
using Microsoft.Extensions.DependencyInjection;

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
                var processor = ResolveProcessor(exportLog, resolver);
                await using var result = await processor.ExportAsync(exportLog, resolver, ct);
                var savePath = $"Export\\{exportLog.CorpId}\\{DateTime.UtcNow:yyyyMMdd}\\{result.FileName}";
                var storage = resolver.Resolve<IStorageProvider>();
                if (!storage.Upload(result.Content, savePath))
                {
                    throw new InvalidOperationException("上传导出文件失败");
                }

                var downloadUrl = $"{storage.Setting.BaseUrl.TrimEnd('/')}/{savePath.TrimStart('/', '\\').Replace("\\", "/")}";
                await exportLogService.MarkSucceededAsync(exportLog.Id, result.FileName, downloadUrl, result.TotalCount, exportLog.ActualFormat);
                await PublishSuccessMessageAsync(exportLog, result, downloadUrl, resolver, ct);
            }
            catch (Exception ex)
            {
                await exportLogService.MarkFailedAsync(exportLog.Id, ex.Message);
                await PublishFailedMessageAsync(exportLog, ex.Message, resolver, ct);
            }
        }

        private static IExportProcessor ResolveProcessor(ExportLog exportLog, IResolver resolver)
        {
            var processorId = ExportProcessorIds.FromExportType(exportLog.ExportType);
            return resolver.ResolveExport<IExportProcessor>(processorId);
        }

        private static async Task PublishSuccessMessageAsync(ExportLog exportLog, ExportFileBuilder.ExportFileResult result, string downloadUrl, IResolver resolver, CancellationToken ct)
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
                Title = $"[{GetExportFormName(exportLog.ExportType, result.FormName)}]导出已完成",
                Detail = $"{GetExportFormName(exportLog.ExportType, result.FormName)}导出已完成，共 {result.ToString} 条",
                Url = downloadUrl,
                ExpireTime = DateTime.UtcNow.AddDays(30).ToTimeStampMs(),
                Category = MessageCategory.SystemNotify,
                MessageType = MessageType.ExportNotify,
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
                Title = $"{GetExportFormName(exportLog.ExportType, null)}导出失败",
                Detail = errorMessage,
                Url = string.Empty,
                ExpireTime = DateTime.UtcNow.AddDays(30).ToTimeStampMs(),
                Category = MessageCategory.SystemNotify,
                MessageType = MessageType.ExportNotify,
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
            var empId = exportLog.CreateBy?.Id;
            if (string.IsNullOrWhiteSpace(empId))
            {
                return null;
            }

            return resolver.Resolve<IRepository<Employee>>().Get(empId);
        }

        private static string GetExportFormName(ExportType exportType, string? formName)
        {
            if (!string.IsNullOrEmpty(formName))
                return formName;

            return exportType switch
            {
                ExportType.AuditLogin => "登录日志",
                ExportType.AuditLog => "操作日志",
                ExportType.FormData => "表单数据",
                _ => "导出",
            };
        }

    }
}
