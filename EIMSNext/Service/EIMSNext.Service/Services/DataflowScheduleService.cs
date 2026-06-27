using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
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
    public class DataflowScheduleService(IResolver resolver) : IDataflowScheduleService
    {
        private readonly IRepository<DataflowScheduleItem> _scheduleRepository = resolver.GetRepository<DataflowScheduleItem>();
        private readonly IRepository<FormData> _formDataRepository = resolver.GetRepository<FormData>();
        private readonly EIMSNext.Scripting.IScriptEngine _scriptEngine = resolver.Resolve<EIMSNext.Scripting.IScriptEngine>();

        /// <inheritdoc />
        public async Task RebuildScheduleAsync(Wf_Definition definition, IClientSessionHandle? session = null)
        {
            await _scheduleRepository.DeleteAsync(_scheduleRepository.FilterBuilder.Eq(x => x.DataflowId, definition.Id), session);

            if (definition.Disabled || definition.EventSource != EventSourceType.Schedule)
            {
                return;
            }

            var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.DfNodeSetting?.TriggerSetting;
            var timeTrigger = triggerSetting?.TimeTrigger;
            if (timeTrigger == null)
            {
                return;
            }

            var scheduleVersion = (definition.UpdateTime ?? 0) > 0 ? definition.UpdateTime!.Value : definition.CreateTime;

            if (timeTrigger.SourceType == DataflowScheduleSourceType.Custom && timeTrigger.StartTime.HasValue)
            {
                var startTime = timeTrigger.StartTime.Value;
                var nextTime = RepeatScheduleCalculator.CalculateNextTriggerTime(timeTrigger.ToTimeTriggerParameter(startTime));
                await _scheduleRepository.InsertAsync(new DataflowScheduleItem
                {
                    CorpId = definition.CorpId,
                    AppId = definition.AppId,
                    DataflowId = definition.Id,
                    FormId = definition.SourceId,
                    TriggerTime = nextTime ?? startTime,
                    AnchorTime = startTime,
                    ScheduleVersion = scheduleVersion,
                    SourceType = DataflowScheduleSourceType.Custom,
                }, session);
                return;
            }

            if (timeTrigger.SourceType == DataflowScheduleSourceType.FormField && !string.IsNullOrWhiteSpace(timeTrigger.TimeField))
            {
                await RebuildFieldSchedulesAsync(definition, timeTrigger, scheduleVersion, session);
            }
        }

        private async Task RebuildFieldSchedulesAsync(Wf_Definition definition, DataflowTimeTriggerSetting timeTrigger, long scheduleVersion, IClientSessionHandle? session)
        {
            var triggerSetting = definition.Metadata.Steps.FirstOrDefault()?.DfNodeSetting?.TriggerSetting;
            var filters = new List<DynamicFilter>
            {
                new() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = definition.CorpId },
                new() { Field = Fields.AppId, Op = FilterOp.Eq, Value = definition.AppId },
                new() { Field = Fields.FormId, Op = FilterOp.Eq, Value = definition.SourceId }
            };
            var mongoFilter = ToMongoFilter(new DynamicFilter { Rel = FilterRel.And, Items = filters });

            var items = new List<DataflowScheduleItem>();
            await _formDataRepository.Find(new MongoFindOptions<FormData> { Filter = mongoFilter }).ForEachAsync(data =>
            {
                if (!IsMeetTriggerCondition(triggerSetting, data))
                {
                    return;
                }

                var rawAnchor = FormNotifyRuntime.ExtractTimeFieldValue(data, timeTrigger.TimeField!);
                if (!rawAnchor.HasValue)
                {
                    return;
                }

                var fieldDate = rawAnchor.Value.ToDateTimeMs();
                var anchor = FormNotifyScheduleCalculator.ResolveFieldAnchor(fieldDate, timeTrigger.FieldFormat, timeTrigger.FixedTime);
                anchor = FormNotifyScheduleCalculator.ApplyOffset(anchor, timeTrigger.Direction, timeTrigger.OffsetValue, timeTrigger.OffsetUnit);
                var anchorMs = DateTime.SpecifyKind(anchor, DateTimeKind.Utc).ToTimeStampMs();
                var nextTime = RepeatScheduleCalculator.CalculateNextTriggerTime(timeTrigger.ToTimeTriggerParameter(anchorMs));
                if (!nextTime.HasValue)
                {
                    return;
                }

                items.Add(new DataflowScheduleItem
                {
                    CorpId = definition.CorpId,
                    AppId = definition.AppId,
                    DataflowId = definition.Id,
                    FormId = data.FormId,
                    DataId = data.Id,
                    TriggerTime = nextTime.Value,
                    AnchorTime = anchorMs,
                    ScheduleVersion = scheduleVersion,
                    SourceType = DataflowScheduleSourceType.FormField,
                });
            });

            if (items.Count > 0)
            {
                await _scheduleRepository.InsertAsync(items, session);
            }
        }

        private static FilterDefinition<FormData> ToMongoFilter(DynamicFilter filter)
        {
            if (filter.Items?.Count > 0)
            {
                var subFilters = filter.Items.Select(ToMongoFilter).ToList();
                return string.Equals(filter.Rel, FilterRel.Or, StringComparison.OrdinalIgnoreCase)
                    ? Builders<FormData>.Filter.Or(subFilters)
                    : Builders<FormData>.Filter.And(subFilters);
            }

            return filter.Op switch
            {
                FilterOp.Eq => Builders<FormData>.Filter.Eq(filter.Field, MongoDB.Bson.BsonValue.Create(filter.Value)),
                _ => Builders<FormData>.Filter.Empty
            };
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
