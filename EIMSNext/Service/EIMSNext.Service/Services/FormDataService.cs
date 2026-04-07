using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Cache;
using EIMSNext.CloudEvent;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Extensions;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

using HKH.Common;
using HKH.Mef2.Integration;

using MongoDB.Driver;
using StackExchange.Redis;

using FlowCascadeMode = EIMSNext.ApiClient.Flow.CascadeMode;

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

            var dataLog = new DataUpdateLog();
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
            var eventHub = Resolver.Resolve<IEventHub>();
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var entity = entities.First();
            var webhookResp = Resolver.GetRepository<Webhook>();
            var webhooks = webhookResp.Find(new MongoFindOptions<Webhook> { Filter = webhookResp.FilterBuilder.And(webhookResp.FilterBuilder.Eq(x => x.FormId, entity.FormId), webhookResp.FilterBuilder.BitsAllSet(x => x.Triggers, (long)WebHookTrigger.Data_Created)) }).ToList();
            if (webhooks.Count > 0)
            {
                var testhook = webhooks.First();
                await eventHub.SendAsync(testhook, WebHookTrigger.Data_Created, entity);
            }

            await EnqueueFormNotify(messagePublisher, entity, null, FormNotifyTriggerMode.DataAdded);
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
            var eventHub = Resolver.Resolve<IEventHub>();
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var old = SessionStore.Get<FormData>(entity.Id, DataVersion.Old);
            var webhookResp = Resolver.GetRepository<Webhook>();
            var webhooks = webhookResp.Find(new MongoFindOptions<Webhook> { Filter = webhookResp.FilterBuilder.And(webhookResp.FilterBuilder.Eq(x => x.FormId, entity.FormId), webhookResp.FilterBuilder.BitsAllSet(x => x.Triggers, (long)WebHookTrigger.Data_Updated)) }).ToList();
            if (webhooks.Count > 0)
            {
                var testhook = webhooks.First();
                var changeLog = ExpandoComparer.Compare(old.Data, entity.Data);
                var oriValue = new ExpandoObject();
                changeLog.ForEach(x => oriValue.TryAdd(x.FieldId, x.OriValue));

                var formExp = entity.SerializeToJson().DeserializeFromJson<ExpandoObject>()!;
                formExp.TryAdd("oridata", oriValue);

                await eventHub.SendAsync(testhook, WebHookTrigger.Data_Created, formExp);
            }

            await EnqueueFormNotify(messagePublisher, entity, old, FormNotifyTriggerMode.DataChanged);

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
                        var dfResp = await _flowClient.RunDataflow(new DfRunRequest { DataId = entity.Id, Trigger = DfTrigger.Submit }, Context.AccessToken);
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
            return messagePublisher.PublishAsync(new FormNotifyDispatchTaskArgs
            {
                CorpId = Context.CorpId,
                DataId = newData.Id,
                TriggerMode = triggerMode,
                Operator = Context.Operator,
                NewData = newData.SerializeToJson().DeserializeFromJson<FormData>()!,
                OldData = oldData?.SerializeToJson().DeserializeFromJson<FormData>()
            });
        }
    }
}
