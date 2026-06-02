using System.Dynamic;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common;
using EIMSNext.Cache;
using EIMSNext.Core;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Common;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace EIMSNext.Service
{
    public class FormDataService : EntityServiceBase<FormData>, IFormDataService
    {
        private FlowApiClient _flowClient;
        public FormDataService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        protected override void CreateAuditLog(DbAction action, IEnumerable<FormData>? oldData, IEnumerable<FormData>? newData, FilterDefinition<FormData>? filter, UpdateDefinition<FormData>? update, IClientSessionHandle? session)
        {
            if (oldData == null || !oldData.Any())
            {
                //新增
            }
            else if (newData == null || !newData.Any())
            {
                //删除
            }
            else
            {
                //TODO:此处需要循环
                var changeLogs = ExpandoComparer.Compare(oldData.First().Data, newData.First().Data);
            }

            var dataLog = new DataChangeLog();
            //TODO: 保存变更日志
            switch (action)
            {
                case DbAction.Insert:
                    break;
                case DbAction.Update:
                    break;
                default: break;
            }
        }

        protected override Task BeforeAdd(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            var formDef = GetFromStore<FormDef>(entities.First().FormId)!;
            if (!formDef.UsingWorkflow)
            {
                //非流程单据直接生效
                entities.ForEach(entity => { entity.FlowStatus = FlowStatus.Approved; });
            }
            return base.BeforeAdd(entities, session);
        }

        public override async Task AddAsync(IEnumerable<FormData> entities)
        {
            await base.AddAsync(entities);
            await SubmitAsync(entities, null, EIMSNext.Service.Entities.CascadeMode.NotSet, null);
        }

        protected override async Task AfterAdd(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var entity = entities.First();
            await EnqueueWebhookAsync(messagePublisher, entity, WebHookTrigger.Data_Created);

            await EnqueueFormNotify(messagePublisher, entity, null, FormNotifyTriggerMode.DataAdded);
            await RebuildTimeFieldNotifySchedulesAsync(entity, session);
            await base.AfterAdd(entities, session);
        }

        public override async Task<ReplaceOneResult> ReplaceAsync(FormData entity)
        {
            var result = await base.ReplaceAsync(entity);
            await SubmitAsync([entity], null, EIMSNext.Service.Entities.CascadeMode.NotSet, null);
            return result;
        }

        protected override async Task AfterReplace(FormData entity, IClientSessionHandle? session)
        {
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var old = ScopeCache.Get<FormData>(entity.Id, DataVersion.Old);
            var oriValue = new ExpandoObject();
            if (old != null)
            {
                var changeLog = ExpandoComparer.Compare(old.Data, entity.Data);
                changeLog.ForEach(x => oriValue.TryAdd(x.FieldId, x.OriValue));
            }

            var formExp = entity.SerializeToJson().DeserializeFromJson<ExpandoObject>()!;
            formExp.TryAdd("oridata", oriValue);
            await EnqueueWebhookAsync(messagePublisher, entity, WebHookTrigger.Data_Updated, formExp);

            await EnqueueFormNotify(messagePublisher, entity, old, FormNotifyTriggerMode.DataChanged);
            await RebuildTimeFieldNotifySchedulesAsync(entity, session);

            await base.AfterReplace(entity, session);
        }

        public async Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, EIMSNext.Service.Entities.CascadeMode cascade, string? eventIds)
        {
            var entity = entities.First();

            if (Context.Action == EIMSNext.Core.Entities.DataAction.Submit)
            {
                var formDef = GetFromStore<FormDef>(entity.FormId)!;

                if (formDef.UsingWorkflow)
                {
                    var wfDef = Resolver.GetRepository<Wf_Definition>().Find(x => x.ExternalId == entity.FormId).FirstOrDefault();
                    if (wfDef != null)
                    {
                        var wfResp = await _flowClient.Start(new StartRequest { WfDefinitionId = entity.FormId, DataId = entity.Id }, Context.AccessToken);
                        if (wfResp != null && !string.IsNullOrEmpty(wfResp.Error))
                        {
                            throw new UnLogException(wfResp.Error);
                        }
                    }
                }
                else
                {
                    if (cascade != EIMSNext.Service.Entities.CascadeMode.Never)
                    {
                        //非流程单据直接提交
                        var dfResp = await _flowClient.RunDataflow(new DfRunRequest { DataId = entity.Id, EventSource = ApiClient.Flow.EventSourceType.Form, EventType = ApiClient.Flow.EventType.Submit }, Context.AccessToken);
                        if (dfResp != null && !string.IsNullOrEmpty(dfResp.Error))
                        {
                            throw new UnLogException(dfResp.Error);
                        }
                    }
                }
            }
        }

        public async Task<FilterOptionResult> GetFieldOptionsAsync(FilterOptionQuery query)
        {
            var rawValues = await Repository.DistinctFieldValuesAsync(query.Filter, query.FieldPath);
            var items = ProcessDistinctValues(rawValues, query.Keyword, query.Limit);
            return new FilterOptionResult { Items = items };
        }

        private static List<FilterOptionItem> ProcessDistinctValues(List<BsonValue> values, string? keyword, int limit)
        {
            var items = new List<FilterOptionItem>();
            foreach (var value in values)
            {
                if (value == null || value.IsBsonNull) continue;

                foreach (var option in ExpandOptionValues(value))
                {
                    if (!string.IsNullOrWhiteSpace(keyword) && option.Label?.Contains(keyword, StringComparison.OrdinalIgnoreCase) != true)
                        continue;

                    if (items.Any(x => x.Id == option.Id))
                        continue;

                    items.Add(option);
                    if (items.Count >= limit) break;
                }

                if (items.Count >= limit) break;
            }

            return items;
        }

        private static IEnumerable<FilterOptionItem> ExpandOptionValues(BsonValue value)
        {
            if (value.IsBsonArray)
            {
                foreach (var item in value.AsBsonArray)
                {
                    foreach (var option in ExpandOptionValues(item))
                        yield return option;
                }
                yield break;
            }

            if (value.IsBsonDocument)
            {
                var doc = value.AsBsonDocument;
                var id = doc.TryGetValue("id", out var idValue) ? idValue.ToString() : value.ToString();
                var label = doc.TryGetValue("label", out var labelValue)
                    ? labelValue.ToString()
                    : doc.TryGetValue("name", out var nameValue)
                        ? nameValue.ToString()
                        : id;

                yield return new FilterOptionItem
                {
                    Id = id,
                    Label = label,
                    Value = BsonTypeMapper.MapToDotNetValue(value)
                };
                yield break;
            }

            var scalar = BsonTypeMapper.MapToDotNetValue(value);
            var text = scalar?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return new FilterOptionItem
                {
                    Id = text,
                    Label = text,
                    Value = scalar
                };
            }
        }

        private Task EnqueueFormNotify(IMessagePublisher messagePublisher, FormData newData, FormData? oldData, FormNotifyTriggerMode triggerMode)
        {
            return messagePublisher.PublishAsync(new NotifyDispatchTaskArgs
            {
                CorpId = Context.CorpId,
                MessageType = MessageType.FormNotify,
                AppId = newData.AppId,
                FormId = newData.FormId,
                DataId = newData.Id,
                FormTriggerMode = triggerMode,
                Operator = Context.Operator,
                NewData = newData.SerializeToJson().DeserializeFromJson<FormData>()!,
                OldData = oldData?.SerializeToJson().DeserializeFromJson<FormData>()
            });
        }

        private static Task EnqueueWebhookAsync(IMessagePublisher messagePublisher, FormData entity, WebHookTrigger trigger, object? payload = null)
        {
            return messagePublisher.PublishAsync(new WebhookTaskArgs
            {
                CorpId = entity.CorpId ?? string.Empty,
                AppId = entity.AppId,
                FormId = entity.FormId,
                Trigger = trigger,
                PayloadJson = (payload ?? entity).SerializeToJson()
            });
        }

        private async Task RebuildTimeFieldNotifySchedulesAsync(FormData entity, IClientSessionHandle? session)
        {
            var notifyRepo = Resolver.GetRepository<FormNotify>();
            var scheduleRepo = Resolver.GetRepository<FormNotifyScheduleItem>();
            var formDef = GetFromStore<FormDef>(entity.FormId);
            if (formDef == null)
            {
                return;
            }

            var notifies = notifyRepo.Find(x =>
                x.CorpId == entity.CorpId &&
                x.AppId == entity.AppId &&
                x.FormId == entity.FormId &&
                !x.Disabled &&
                x.TriggerMode == FormNotifyTriggerMode.TimeFieldScheduled).ToList();

            foreach (var notify in notifies)
            {
                await scheduleRepo.DeleteAsync(scheduleRepo.FilterBuilder.And(
                    scheduleRepo.FilterBuilder.Eq(x => x.NotifyId, notify.Id),
                    scheduleRepo.FilterBuilder.Eq(x => x.DataId, entity.Id)), session);

                if (string.IsNullOrWhiteSpace(notify.TimeField))
                {
                    continue;
                }

                var dataMatches = FormNotifyRuntime.ShouldNotify(this.Resolver, notify, new NotifyDispatchTaskArgs
                {
                    CorpId = entity.CorpId ?? string.Empty,
                    DataId = entity.Id,
                    AppId = entity.AppId,
                    FormId = entity.FormId,
                    FormTriggerMode = FormNotifyTriggerMode.TimeFieldScheduled,
                    NewData = entity
                });
                if (!dataMatches)
                {
                    continue;
                }

                var anchorTime = FormNotifyRuntime.ExtractTimeFieldValue(entity, notify.TimeField);
                if (!anchorTime.HasValue)
                {
                    continue;
                }

                var nextTriggerTime = FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, anchorTime.Value);
                if (!nextTriggerTime.HasValue)
                {
                    continue;
                }

                await scheduleRepo.InsertAsync(new FormNotifyScheduleItem
                {
                    NotifyId = notify.Id,
                    DataId = entity.Id,
                    AppId = entity.AppId,
                    FormId = entity.FormId,
                    CorpId = entity.CorpId,
                    TriggerMode = FormNotifyTriggerMode.TimeFieldScheduled,
                    ScheduleVersion = notify.ScheduleVersion,
                    TriggerTime = nextTriggerTime.Value,
                    AnchorTime = anchorTime.Value,
                    TimeField = notify.TimeField
                }, session);
            }
        }
    }
}
