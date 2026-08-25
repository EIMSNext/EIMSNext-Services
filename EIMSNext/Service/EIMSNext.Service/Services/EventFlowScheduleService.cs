using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using MongoDB.Driver;

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
        public async Task RebuildScheduleAsync(Wf_Definition definition, IClientSessionHandle? session = null)
        {
            await _scheduleRepository.DeleteAsync(_scheduleRepository.FilterBuilder.Eq(x => x.EventFlowId, definition.Id), session);

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
                }, session);
                return;
            }

            if (timeTrigger.SourceType == EventFlowScheduleSourceType.FormField && !string.IsNullOrWhiteSpace(timeTrigger.TimeField))
            {
                await RebuildFieldSchedulesAsync(definition, timeTrigger, scheduleVersion, session);
            }
        }

        private async Task RebuildFieldSchedulesAsync(Wf_Definition definition, EventFlowTimeTriggerSetting timeTrigger, long scheduleVersion, IClientSessionHandle? session)
        {
            var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.EfNodeSetting?.TriggerSetting;
            var filters = new List<DynamicFilter>
            {
                new() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = definition.CorpId },
                new() { Field = Fields.AppId, Op = FilterOp.Eq, Value = definition.AppId },
                new() { Field = Fields.FormId, Op = FilterOp.Eq, Value = definition.SourceId }
            };
            var mongoFilter = new DynamicFilter { Rel = FilterRel.And, Items = filters }
                .ToFilterDefinition<FormData>();

            var items = new List<EventFlowScheduleItem>();
            var find = session == null
                ? _formDataRepository.Collection.Find(mongoFilter)
                : _formDataRepository.Collection.Find(session, mongoFilter);
            using var cursor = await find.ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (var data in cursor.Current)
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
                        await _scheduleRepository.InsertAsync(items, session);
                        items.Clear();
                    }
                }
            }

            if (items.Count > 0)
            {
                await _scheduleRepository.InsertAsync(items, session);
            }
        }

        private bool IsMeetTriggerCondition(TriggerSetting? triggerSetting, FormData data)
        {
            if (string.IsNullOrWhiteSpace(triggerSetting?.Condition))
            {
                return true;
            }

            return _scriptEngine.Evaluate<bool>(triggerSetting.Condition, ToScriptData(data)).Value;
        }

        private static Dictionary<string, object> ToScriptData(FormData formData)
        {
            IDictionary<string, object?> formDataWrapper = new ExpandoObject();
            formDataWrapper["createBy"] = formData.CreateBy;

            foreach (var item in (IDictionary<string, object?>)formData.Data)
            {
                formDataWrapper[item.Key] = item.Value;
            }

            IDictionary<string, object?> dataWrapper = new ExpandoObject();
            dataWrapper[$"f_{formData.FormId}"] = formDataWrapper;

            return new Dictionary<string, object> { ["data"] = dataWrapper };
        }
    }
}
