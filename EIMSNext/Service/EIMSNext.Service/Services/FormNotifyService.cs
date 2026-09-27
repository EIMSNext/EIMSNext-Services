using EIMSNext.Component;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Common;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace EIMSNext.Service
{
    public class FormNotifyService(IResolver resolver) : EntityServiceBase<FormNotify>(resolver), IFormNotifyService
    {
        private const int ScheduleInsertBatchSize = 500;

        private IRepository<FormNotifyScheduleItem> ScheduleRepository => Resolver.GetRepository<FormNotifyScheduleItem>();

        protected override Task BeforeAdd(IEnumerable<FormNotify> entities, IClientSessionHandle? session)
        {
            var entity = entities.First();

            PrepareEntity(entity);

            return Task.CompletedTask;
        }

        protected override Task BeforeReplace(FormNotify entity, IClientSessionHandle? session)
        {
            PrepareEntity(entity);

            return Task.CompletedTask;
        }

        protected override async Task AfterAdd(IEnumerable<FormNotify> entities, IClientSessionHandle? session)
        {
            foreach (var entity in entities)
            {
                await RebuildScheduleAsync(entity, session);
            }

            await base.AfterAdd(entities, session);
        }

        protected override async Task AfterReplace(FormNotify entity, IClientSessionHandle? session)
        {
            await RebuildScheduleAsync(entity, session);
            await base.AfterReplace(entity, session);
        }

        protected override Task BeforeDelete(FilterDefinition<FormNotify> filter, IClientSessionHandle? session)
        {
            var deletingNotifyIds = Repository.Find(new MongoFindOptions<FormNotify> { Filter = filter }, session)
                .Project(x => x.Id)
                .ToList();

            if (deletingNotifyIds.Count > 0)
            {
                return ScheduleRepository.DeleteAsync(ScheduleRepository.FilterBuilder.In(x => x.NotifyId, deletingNotifyIds), session);
            }

            return base.BeforeDelete(filter, session);
        }

        private void PrepareEntity(FormNotify entity)
        {
            ValidateEntity(entity);
            ParseDataFilter(entity);
            entity.ScheduleVersion = (entity.ScheduleVersion <= 0 ? 0 : entity.ScheduleVersion) + 1;
            entity.LastTriggerTime = null;
            entity.NextTriggerTime = entity.Disabled || entity.TriggerMode != FormNotifyTriggerMode.CustomScheduled || !entity.StartTime.HasValue
                ? null
                : FormNotifyScheduleCalculator.CalculateNextTriggerTime(entity, entity.StartTime.Value);
        }

        private void ValidateEntity(FormNotify entity)
        {
            if (entity.TargetType == NotifyTargetType.Dashboard)
            {
                ValidateDashboardNotify(entity);
                return;
            }

            switch (entity.TriggerMode)
            {
                case FormNotifyTriggerMode.CustomScheduled:
                    ValidateCustomScheduled(entity);
                    break;
                case FormNotifyTriggerMode.TimeFieldScheduled:
                    ValidateTimeFieldScheduled(entity);
                    break;
                default:
                    entity.TimeField = null;
                    entity.StartTime = null;
                    entity.EndTime = null;
                    entity.RepeatType = null;
                    entity.RepeatConfig = null;
                    entity.NextTriggerTime = null;
                    entity.LastTriggerTime = null;
                    break;
            }
        }

        private static void ValidateCustomScheduled(FormNotify entity)
        {
            if (!entity.StartTime.HasValue)
            {
                throw new BadRequestException("自定义提醒必须设置开始提醒时间");
            }

            if (!entity.RepeatType.HasValue)
            {
                throw new BadRequestException("自定义提醒必须设置重复类型");
            }

            if (entity.EndTime.HasValue && entity.EndTime.Value < entity.StartTime.Value)
            {
                throw new BadRequestException("结束提醒时间不能早于开始提醒时间");
            }

            if (FormNotifyScheduleCalculator.ContainsFieldTokens(entity.NotifyText))
            {
                throw new BadRequestException("自定义提醒文字不支持插入表单字段");
            }

            var notifiers = (entity.Notifiers ?? "[]").DeserializeFromJson<List<ApprovalCandidate>>() ?? [];
            if (notifiers.Any(x => x.CandidateType == CandidateType.FormField))
            {
                throw new BadRequestException("自定义提醒不支持通过表单字段选择提醒人");
            }

            entity.TimeField = null;
            entity.ChangeFields = [];
            entity.DataFilter = null;
            entity.DataDynamicFilter = null;
            entity.DataExpressFilter = null;
        }

        private static void ValidateDashboardNotify(FormNotify entity)
        {
            if (entity.TriggerMode != FormNotifyTriggerMode.CustomScheduled)
            {
                throw new BadRequestException("仪表盘提醒仅支持自定义定时提醒");
            }

            ValidateCustomScheduled(entity);
            entity.TimeField = null;
            entity.FixedTime = null;
            entity.Direction = TimerOffsetDirection.At;
            entity.OffsetValue = null;
            entity.OffsetUnit = null;
            entity.FieldFormat = null;
            entity.ChangeFields = [];
            entity.DataFilter = null;
            entity.DataDynamicFilter = null;
            entity.DataExpressFilter = null;
        }

        private void ValidateTimeFieldScheduled(FormNotify entity)
        {
            if (string.IsNullOrWhiteSpace(entity.TimeField))
            {
                throw new BadRequestException("字段提醒必须选择日期时间字段");
            }

            if (!entity.RepeatType.HasValue)
            {
                throw new BadRequestException("字段提醒必须设置重复类型");
            }

            var formDef = GetFromStore<FormDef>(entity.FormId);
            var timeField = ResolveTimeFieldDef(formDef, entity.TimeField!);
            if (timeField == null || !string.Equals(timeField.Type, FieldType.TimeStamp, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("字段提醒只能选择日期时间字段");
            }

            entity.StartTime = null;
            entity.ChangeFields = [];
        }

        private async Task RebuildScheduleAsync(FormNotify entity, IClientSessionHandle? session)
        {
            await ScheduleRepository.DeleteAsync(ScheduleRepository.FilterBuilder.Eq(x => x.NotifyId, entity.Id), session);
            if (entity.Disabled)
            {
                return;
            }

            if (entity.TriggerMode == FormNotifyTriggerMode.CustomScheduled)
            {
                if (!entity.NextTriggerTime.HasValue || !entity.StartTime.HasValue)
                {
                    return;
                }

                await ScheduleRepository.InsertAsync(new FormNotifyScheduleItem
                {
                    NotifyId = entity.Id,
                    CorpId = entity.CorpId,
                    AppId = entity.AppId,
                    FormId = entity.FormId,
                    TargetType = entity.TargetType,
                    TriggerMode = entity.TriggerMode,
                    ScheduleVersion = entity.ScheduleVersion,
                    TriggerTime = entity.NextTriggerTime.Value,
                    AnchorTime = entity.StartTime.Value
                }, session);
                return;
            }

            if (entity.TriggerMode != FormNotifyTriggerMode.TimeFieldScheduled)
            {
                return;
            }

            var formDataRepo = Resolver.GetRepository<FormData>();
            var filter = BuildTimeFieldDataFilter(entity);
            var items = new List<FormNotifyScheduleItem>();
            var find = session == null
                ? formDataRepo.Collection.Find(filter)
                : formDataRepo.Collection.Find(session, filter);
            using var cursor = await find.ToCursorAsync();
            while (await cursor.MoveNextAsync())
            {
                foreach (var data in cursor.Current)
                {
                    var rawAnchor = FormNotifyRuntime.ExtractTimeFieldValue(data, entity.TimeField!);
                    if (!rawAnchor.HasValue)
                    {
                        continue;
                    }

                    var anchor = FormNotifyRuntime.ResolveAdjustedAnchor(entity, rawAnchor.Value) ?? rawAnchor.Value;
                    var nextTriggerTime = FormNotifyScheduleCalculator.CalculateNextTriggerTime(entity, anchor);
                    if (!nextTriggerTime.HasValue)
                    {
                        continue;
                    }

                    items.Add(new FormNotifyScheduleItem
                    {
                        NotifyId = entity.Id,
                        CorpId = entity.CorpId,
                        AppId = entity.AppId,
                        FormId = entity.FormId,
                        TargetType = entity.TargetType,
                        DataId = data.Id,
                        TriggerMode = entity.TriggerMode,
                        ScheduleVersion = entity.ScheduleVersion,
                        TriggerTime = nextTriggerTime.Value,
                        AnchorTime = anchor,
                        TimeField = entity.TimeField
                    });

                    if (items.Count >= ScheduleInsertBatchSize)
                    {
                        await ScheduleRepository.InsertAsync(items, session);
                        items.Clear();
                    }
                }
            }

            if (items.Count > 0)
            {
                await ScheduleRepository.InsertAsync(items, session);
            }
        }

        private FilterDefinition<FormData> BuildTimeFieldDataFilter(FormNotify entity)
        {
            var filters = new List<DynamicFilter>
            {
                new() { Field = Fields.CorpId, Op = FilterOp.Eq, Value = entity.CorpId },
                new() { Field = Fields.AppId, Op = FilterOp.Eq, Value = entity.AppId },
                new() { Field = Fields.FormId, Op = FilterOp.Eq, Value = entity.FormId }
            };

            if (!string.IsNullOrWhiteSpace(entity.DataDynamicFilter))
            {
                var dynamicFilter = entity.DataDynamicFilter.DeserializeFromJson<DynamicFilter>();
                if (dynamicFilter != null)
                {
                    filters.Add(dynamicFilter);
                }
            }

            return new DynamicFilter { Rel = FilterRel.And, Items = filters }
                .ToFilterDefinition<FormData>();
        }

        private static FieldDef? ResolveTimeFieldDef(FormDef? formDef, string timeField)
        {
            if (string.Equals(timeField, Fields.CreateTime, StringComparison.OrdinalIgnoreCase))
            {
                return new FieldDef { Field = Fields.CreateTime, Type = FieldType.TimeStamp, Title = "提交时间" };
            }

            if (string.Equals(timeField, Fields.UpdateTime, StringComparison.OrdinalIgnoreCase))
            {
                return new FieldDef { Field = Fields.UpdateTime, Type = FieldType.TimeStamp, Title = "更新时间" };
            }

            return formDef?.Content?.Items?.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Field) &&
                x.Field.Equals(timeField, StringComparison.OrdinalIgnoreCase));
        }

        private void ParseDataFilter(FormNotify entity)
        {
            if (entity.TriggerMode == FormNotifyTriggerMode.CustomScheduled)
            {
                entity.DataDynamicFilter = null;
                entity.DataExpressFilter = null;
                return;
            }

            if (!string.IsNullOrEmpty(entity.DataFilter))
            {
                var condList = entity.DataFilter.DeserializeFromJson<ConditionList>();
                if (condList != null)
                {
                    if (entity.TriggerMode == FormNotifyTriggerMode.DataAdded || entity.TriggerMode == FormNotifyTriggerMode.DataChanged)
                    {
                        entity.DataDynamicFilter = null;
                        entity.DataExpressFilter = condList.ToScriptExpression();
                    }
                    else
                    {
                        entity.DataDynamicFilter = condList.ToDynamicFilter().SerializeToJson();
                        entity.DataExpressFilter = null;
                    }
                }
                else
                {
                    entity.DataDynamicFilter = null;
                    entity.DataExpressFilter = null;
                }
            }
            else
            {
                entity.DataDynamicFilter = null;
                entity.DataExpressFilter = null;
            }
        }
    }
}
