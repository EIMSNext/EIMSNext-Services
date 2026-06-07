using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
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
                    await scheduleRepo.DeleteAsync(item.Id);
                    continue;
                }

                if (notify.EndTime.HasValue && item.TriggerTime > notify.EndTime.Value)
                {
                    await scheduleRepo.DeleteAsync(item.Id);
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
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        private static async Task AdvanceScheduleAsync(IRepository<FormNotifyScheduleItem> scheduleRepo, IRepository<FormNotify> notifyRepo, FormNotifyScheduleItem item, FormNotify notify)
        {
            var next = FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, item.AnchorTime, item.TriggerTime);
            if (notify.TriggerMode == FormNotifyTriggerMode.CustomScheduled)
            {
                var notifyUpdate = notifyRepo.UpdateBuilder
                    .Set(x => x.LastTriggerTime, item.TriggerTime)
                    .Set(x => x.NextTriggerTime, next);
                await notifyRepo.UpdateAsync(notify.Id, notifyUpdate, false);
            }

            if (next.HasValue)
            {
                item.TriggerTime = next.Value;
                await scheduleRepo.ReplaceAsync(item);
            }
            else
            {
                await scheduleRepo.DeleteAsync(item.Id);
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
                    DataId = string.Empty,
                    FormTriggerMode = FormNotifyTriggerMode.CustomScheduled,
                    Operator = Operator.Empty,
                    NewData = new FormData
                    {
                        AppId = notify.AppId,
                        FormId = notify.FormId,
                        CorpId = notify.CorpId,
                        Data = new System.Dynamic.ExpandoObject()
                    }
                });
            }

            return publisher.PublishAsync(new NotifyDispatchTaskArgs
            {
                CorpId = notify.CorpId ?? string.Empty,
                MessageType = MessageType.FormNotify,
                AppId = notify.AppId,
                FormId = notify.FormId,
                DataId = item.DataId ?? string.Empty,
                FormTriggerMode = FormNotifyTriggerMode.TimeFieldScheduled,
                Operator = Operator.Empty,
                NewData = new FormData
                {
                    Id = item.DataId ?? string.Empty,
                    AppId = notify.AppId,
                    FormId = notify.FormId,
                    CorpId = notify.CorpId,
                    Data = new System.Dynamic.ExpandoObject()
                }
            });
        }
    }
}
