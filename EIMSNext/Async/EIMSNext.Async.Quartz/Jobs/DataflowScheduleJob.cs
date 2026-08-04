using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    /// <summary>
    /// 数据流定时调度扫描作业。扫描到到期项后通过 IMessagePublisher 投递 DataflowRunTaskArgs，
    /// 由 Async.Tasks 中的 DataflowRunConsumer 调 FlowApiClient.RunDataflow 触发一次执行。
    /// </summary>
    [DisallowConcurrentExecution]
    public class DataflowScheduleJob : JobBase<DataflowScheduleJob>
    {
        public DataflowScheduleJob(IResolver resolver) : base(resolver)
        {
        }

        protected override Task ExecuteAsync(IJobExecutionContext context)
        {
            return ExecuteInternalAsync();
        }

        private async Task ExecuteInternalAsync()
        {
            var scheduleRepo = Resolver.GetRepository<DataflowScheduleItem>();
            var definitionRepo = Resolver.GetRepository<Wf_Definition>();
            var formDataRepo = Resolver.GetRepository<FormData>();
            var publisher = Resolver.Resolve<IMessagePublisher>();
            var now = DateTime.UtcNow.ToTimeStampMs();

            var dueItems = scheduleRepo.Find(x => x.TriggerTime <= now).ToList();
            Logger.LogInformation("Dataflow schedule scan found {Count} due items", dueItems.Count);

            foreach (var item in dueItems)
            {
                try
                {
                    var definition = definitionRepo.Get(item.DataflowId);
                    if (definition == null || definition.Disabled
                        || definition.EventSource != EventSourceType.Schedule)
                    {
                        await scheduleRepo.DeleteAsync(item.Id);
                        continue;
                    }

                    var currentVersion = (definition.UpdateTime ?? 0) > 0 ? definition.UpdateTime!.Value : definition.CreateTime;
                    if (currentVersion != item.ScheduleVersion)
                    {
                        await scheduleRepo.DeleteAsync(item.Id);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(item.DataId))
                    {
                        var data = formDataRepo.Get(item.DataId);
                        if (data == null)
                        {
                            await scheduleRepo.DeleteAsync(item.Id);
                            continue;
                        }
                    }

                    var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.DfNodeSetting?.TriggerSetting;
                    var wfNodeId = triggerSetting?.WfNodeId ?? string.Empty;

                    await publisher.PublishAsync(new DataflowRunTaskArgs
                    {
                        CorpId = definition.CorpId??string.Empty,
                        DataflowId = definition.Id,
                        AppId = definition.AppId,
                        FormId = item.FormId,
                        DataId = item.DataId,
                        EventSource = EventSourceType.Schedule,
                        EventType = EventType.None,
                        Cascade = CascadeMode.All,
                        WfNodeId = string.IsNullOrEmpty(wfNodeId) ? null : wfNodeId,
                    });

                    await AdvanceAsync(scheduleRepo, definition, item);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Dataflow schedule execution failed. ScheduleId={Id}, DataflowId={DataflowId}", item.Id, item.DataflowId);
                }
            }
        }

        private async Task AdvanceAsync(IRepository<DataflowScheduleItem> scheduleRepo, Wf_Definition definition, DataflowScheduleItem item)
        {
            var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.DfNodeSetting?.TriggerSetting;
            var timeTrigger = triggerSetting?.TimeTrigger;
            if (timeTrigger == null)
            {
                await scheduleRepo.DeleteAsync(item.Id);
                return;
            }

            var next = RepeatScheduleCalculator.CalculateNextTriggerTime(timeTrigger.ToTimeTriggerParameter(item.AnchorTime, item.TriggerTime));
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
    }
}
