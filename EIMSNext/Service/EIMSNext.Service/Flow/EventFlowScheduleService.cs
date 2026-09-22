using EIMSNext.Common.Extensions;
using EIMSNext.Component;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using EIMSNext.Common;
using System.Dynamic;

namespace EIMSNext.Service
{
    /// <summary>
    /// 数据流定时调度服务实现。
    /// </summary>
    public class EventFlowScheduleService(IResolver resolver) : IEventFlowScheduleService
    {
        private const int ScheduleInsertBatchSize = 500;

        private readonly IRepository<EventFlowScheduleItem> _scheduleRepository = resolver.GetRepository<EventFlowScheduleItem>();
        private readonly IRepository<FormData> _formDataRepository = resolver.GetRepository<FormData>();
        private readonly EIMSNext.Scripting.IScriptEngine _scriptEngine = resolver.Resolve<EIMSNext.Scripting.IScriptEngine>();

        /// <inheritdoc />
        public async Task RebuildScheduleAsync(Wf_Definition definition)
        {
            await _scheduleRepository.DeleteManyAsync(x => x.EventFlowId == definition.Id);

            if (definition.Disabled || definition.EventSource != EventSourceType.Schedule)
            {
                return;
            }

            var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.EfNodeSetting?.TriggerSetting;
            var timeTrigger = triggerSetting?.TimeTrigger;
            if (timeTrigger == null)
            {
                return;
            }

            var scheduleVersion = (definition.UpdateTime ?? 0) > 0 ? definition.UpdateTime!.Value : definition.CreateTime;

            if (timeTrigger.SourceType == EventFlowScheduleSourceType.Custom && timeTrigger.StartTime.HasValue)
            {
                var startTime = timeTrigger.StartTime.Value;
                var nextTime = RepeatScheduleCalculator.CalculateNextTriggerTime(timeTrigger.ToTimeTriggerParameter(startTime));
                await _scheduleRepository.InsertAsync(new EventFlowScheduleItem
                {
                    CorpId = definition.CorpId,
                    AppId = definition.AppId,
                    EventFlowId = definition.Id,
                    FormId = definition.SourceId,
                    TriggerTime = nextTime ?? startTime,
                    AnchorTime = startTime,
                    ScheduleVersion = scheduleVersion,
                    SourceType = EventFlowScheduleSourceType.Custom,
                });
                return;
            }

            if (timeTrigger.SourceType == EventFlowScheduleSourceType.FormField && !string.IsNullOrWhiteSpace(timeTrigger.TimeField))
            {
                await RebuildFieldSchedulesAsync(definition, timeTrigger, scheduleVersion);
            }
        }

        private async Task RebuildFieldSchedulesAsync(Wf_Definition definition, EventFlowTimeTriggerSetting timeTrigger, long scheduleVersion)
        {
            var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.EfNodeSetting?.TriggerSetting;
            var filters = new List<DynamicFilter>
            {
                new() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = definition.CorpId },
                new() { Field = Fields.AppId, Op = FilterOp.Eq, Value = definition.AppId },
                new() { Field = Fields.FormId, Op = FilterOp.Eq, Value = definition.SourceId }
            };
            var predicate = new DynamicFilter { Rel = FilterRel.And, Items = filters }
                .ToPredicate<FormData>();

            var items = new List<EventFlowScheduleItem>();
            foreach (var data in _formDataRepository.Find(predicate))
            {
                if (!IsMeetTriggerCondition(triggerSetting, data))
                {
                    continue;
                }

                var rawAnchor = FormNotifyRuntime.ExtractTimeFieldValue(data, timeTrigger.TimeField!);
                if (!rawAnchor.HasValue)
                {
                    continue;
                }

                var fieldDate = rawAnchor.Value.ToDateTimeMs();
                var anchor = FormNotifyScheduleCalculator.ResolveFieldAnchor(fieldDate, timeTrigger.FieldFormat, timeTrigger.FixedTime);
                anchor = FormNotifyScheduleCalculator.ApplyOffset(anchor, timeTrigger.Direction, timeTrigger.OffsetValue, timeTrigger.OffsetUnit);
                var anchorMs = DateTime.SpecifyKind(anchor, DateTimeKind.Utc).ToTimeStampMs();
                var nextTime = RepeatScheduleCalculator.CalculateNextTriggerTime(timeTrigger.ToTimeTriggerParameter(anchorMs));
                if (!nextTime.HasValue)
                {
                    continue;
                }

                items.Add(new EventFlowScheduleItem
                {
                    CorpId = definition.CorpId,
                    AppId = definition.AppId,
                    EventFlowId = definition.Id,
                    FormId = data.FormId,
                    DataId = data.Id,
                    TriggerTime = nextTime.Value,
                    AnchorTime = anchorMs,
                    ScheduleVersion = scheduleVersion,
                    SourceType = EventFlowScheduleSourceType.FormField,
                });

                if (items.Count >= ScheduleInsertBatchSize)
                {
                    await _scheduleRepository.InsertAsync(items);
                    items.Clear();
                }
            }

            if (items.Count > 0)
            {
                await _scheduleRepository.InsertAsync(items);
            }
        }

        private bool IsMeetTriggerCondition(TriggerSetting? triggerSetting, FormData data)
        {
            if (string.IsNullOrWhiteSpace(triggerSetting?.Condition))
            {
                return true;
            }

            // 复用 Component 层的统一实现（此前本文件有一份私有副本）。
            return _scriptEngine.Evaluate<bool>(triggerSetting.Condition, data.ToScriptData()).Value;
        }
    }
}
