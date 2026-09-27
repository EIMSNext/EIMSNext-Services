using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Entities;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    public class FormNotifyScheduleJob : JobBase<FormNotifyScheduleJob>, IFormNotifyScheduleJob
    {
        public FormNotifyScheduleJob(IResolver resolver) : base(resolver)
        {
        }

        protected override Task ExecuteAsync(IJobExecutionContext context)
        {
            return ExecuteInternalAsync();
        }

        private async Task ExecuteInternalAsync()
        {
            var scheduleRepo = Resolver.GetRepository<FormNotifyScheduleItem>();
            var notifyRepo = Resolver.GetRepository<FormNotify>();
            var dispatchRepo = Resolver.GetRepository<FormNotifyDispatchLog>();
            var publisher = Resolver.Resolve<IMessagePublisher>();
            var now = DateTime.UtcNow.ToTimeStampMs();

            var dueItems = scheduleRepo.Find(x => x.TriggerTime <= now).ToList();
            Logger.LogInformation("Form notify schedule scan found {Count} due items", dueItems.Count);
            foreach (var item in dueItems)
            {
                var notify = notifyRepo.Get(item.NotifyId);
                if (notify == null || notify.Disabled || notify.ScheduleVersion != item.ScheduleVersion)
                {
                    await scheduleRepo.DeleteManyAsync(x => x.Id == item.Id);
                    continue;
                }

                if (notify.EndTime.HasValue && item.TriggerTime > notify.EndTime.Value)
                {
                    await scheduleRepo.DeleteManyAsync(x => x.Id == item.Id);
                    continue;
                }

                if (!await TryCreateDispatchLogAsync(dispatchRepo, item, notify))
                {
                    await AdvanceScheduleAsync(scheduleRepo, notifyRepo, item, notify);
                    continue;
                }

                await PublishDispatchTaskAsync(publisher, item, notify);
                await AdvanceScheduleAsync(scheduleRepo, notifyRepo, item, notify);
            }
        }

        private static async Task<bool> TryCreateDispatchLogAsync(IRepository<FormNotifyDispatchLog> dispatchRepo, FormNotifyScheduleItem item, FormNotify notify)
        {
            try
            {
                await dispatchRepo.InsertAsync(new FormNotifyDispatchLog
                {
                    NotifyId = notify.Id,
                    DataId = item.DataId,
                    TriggerTime = item.TriggerTime,
                    CorpId = notify.CorpId
                });
                return true;
            }
            catch (DbUpdateException ex) when (IsDispatchLogDuplicate(ex))
            {
                return false;
            }
        }

        /// <summary>
        /// 判断异常是否为 <c>FormNotifyDispatchLog</c> 的幂等唯一索引冲突。
        /// </summary>
        private static bool IsDispatchLogDuplicate(DbUpdateException exception)
        {
            var postgres = exception.InnerException as PostgresException
                ?? exception.InnerException?.InnerException as PostgresException;
            if (postgres is null || postgres.SqlState != "23505")
            {
                return false;
            }

            return postgres.ConstraintName?.Contains("FormNotifyDispatchLog", StringComparison.OrdinalIgnoreCase) == true
                || postgres.MessageText.Contains("formnotifydispatchlog", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task AdvanceScheduleAsync(IRepository<FormNotifyScheduleItem> scheduleRepo, IRepository<FormNotify> notifyRepo, FormNotifyScheduleItem item, FormNotify notify)
        {
            var next = FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, item.AnchorTime, item.TriggerTime);
            if (notify.TriggerMode == FormNotifyTriggerMode.CustomScheduled)
            {
                // UpdateAsync(id, setters => setters.SetProperty(...))，同样翻译成单条 UPDATE。
                await notifyRepo.UpdateAsync(
                    notify.Id,
                    setters => setters
                        .SetProperty(x => x.LastTriggerTime, item.TriggerTime)
                        .SetProperty(x => x.NextTriggerTime, next));
            }

            if (next.HasValue)
            {
                item.TriggerTime = next.Value;
                await scheduleRepo.ReplaceAsync(item);
            }
            else
            {
                await scheduleRepo.DeleteManyAsync(x => x.Id == item.Id);
            }
        }

        private static Task PublishDispatchTaskAsync(IMessagePublisher publisher, FormNotifyScheduleItem item, FormNotify notify)
        {
            if (item.TriggerMode == FormNotifyTriggerMode.CustomScheduled)
            {
                return publisher.PublishAsync(new NotifyDispatchTaskArgs
                {
                    CorpId = notify.CorpId ?? string.Empty,
                    MessageType = MessageType.FormNotify,
                    AppId = notify.AppId,
                    FormId = notify.FormId,
                    TargetType = notify.TargetType,
                    DataId = string.Empty,
                    FormTriggerMode = FormNotifyTriggerMode.CustomScheduled,
                    Operator = Operator.Empty,
                    EventStamp = item.TriggerTime,
                    NewData = new FormData
                    {
                        AppId = notify.AppId,
                        FormId = notify.FormId,
                        CorpId = notify.CorpId,
                        Data = new Dictionary<string, object?>()
                    }
                });
            }

            return publisher.PublishAsync(new NotifyDispatchTaskArgs
            {
                CorpId = notify.CorpId ?? string.Empty,
                MessageType = MessageType.FormNotify,
                AppId = notify.AppId,
                FormId = notify.FormId,
                TargetType = notify.TargetType,
                DataId = item.DataId ?? string.Empty,
                FormTriggerMode = FormNotifyTriggerMode.TimeFieldScheduled,
                Operator = Operator.Empty,
                EventStamp = item.TriggerTime,
                NewData = new FormData
                {
                    Id = item.DataId ?? string.Empty,
                    AppId = notify.AppId,
                    FormId = notify.FormId,
                    CorpId = notify.CorpId,
                    Data = new Dictionary<string, object?>()
                }
            });
        }
    }
}
