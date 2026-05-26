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
