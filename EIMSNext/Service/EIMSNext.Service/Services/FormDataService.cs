using System.Dynamic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Common;
using EIMSNext.Cache;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Abstractions.Extensions;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Common;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class FormDataService : EntityServiceBase<FormData>, IFormDataService
    {
        private FlowApiClient _flowClient;
        private readonly ISerialNoSequenceService _serialNoSvc;
        private readonly AttachmentReferenceService _attachmentReferenceService;
        public FormDataService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
            _serialNoSvc = resolver.Resolve<ISerialNoSequenceService>();
            _attachmentReferenceService = new AttachmentReferenceService(resolver);
        }

        protected override List<AuditLog> CreateUpdateLog(IEnumerable<FormData>? oldData, IEnumerable<FormData>? newData, FilterDefinition<FormData>? filter, UpdateDefinition<FormData>? update)
        {
            var logList = new List<AuditLog>();
            var now = DateTime.UtcNow.ToTimeStampMs();
            var op = Context.Operator;
            var ip = Context.ClientIp;
            var corpId = Context.CorpId;

            if (oldData == null || newData == null)
            {
                logList.Add(new AuditLog
                {
                    Action = DbAction.Update,
                    EntityType = nameof(FormData),
                    Detail = $"批量更新数据(无旧对象):{filter?.ToString()}",
                    DataFilter = filter?.ToString(),
                    CreateBy = op,
                    UpdateBy = op,
                    CreateTime = now,
                    UpdateTime = now,
                    ClientIp = ip,
                    CorpId = corpId,
                });
            }
            else
            {
                oldData.ForEach(x =>
                {
                    var y = newData.FirstOrDefault(e => e.Id == x.Id);
                    if (y == null) return;
                    logList.Add(new AuditLog
                    {
                        Action = DbAction.Update,
                        EntityType = nameof(FormData),
                        DataId = x.Id,
                        // FormData 不展开字段差异；详情由 FormDataChangeLog 体现
                        Detail = "已修改（详情见 FormDataChangeLog）",
                        OldData = x.SerializeToJson(),
                        NewData = y.SerializeToJson(),
                        CreateBy = op,
                        UpdateBy = op,
                        CreateTime = now,
                        UpdateTime = now,
                        ClientIp = ip,
                        CorpId = corpId,
                    });
                });
            }

            return logList;
        }

        private static Dictionary<string, FieldDef> BuildFieldLookup(FormDef? formDef)
        {
            var lookup = new Dictionary<string, FieldDef>(StringComparer.OrdinalIgnoreCase);
            if (formDef?.Content?.Items == null) return lookup;

            foreach (var field in formDef.Content.Items)
            {
                AddField(field, null);
            }

            return lookup;

            void AddField(FieldDef field, string? parentField)
            {
                if (string.IsNullOrWhiteSpace(field.Field)) return;

                lookup.TryAdd(field.Field, field);

                if (!string.IsNullOrWhiteSpace(parentField))
                {
                    lookup.TryAdd($"{parentField}>{field.Field}", field);
                }

                if (field.Columns == null) return;
                foreach (var column in field.Columns)
                {
                    AddField(column, field.Field);
                }
            }
        }

        private static DataChangeContent ToDataChangeContent(ExpandoChangeLog changeLog, IReadOnlyDictionary<string, FieldDef> fieldLookup)
        {
            fieldLookup.TryGetValue(changeLog.FieldId, out var fieldDef);

            return new DataChangeContent
            {
                FieldId = changeLog.FieldId,
                FieldLabel = string.IsNullOrWhiteSpace(fieldDef?.Title) ? changeLog.FieldId : fieldDef.Title,
                FieldType = string.IsNullOrWhiteSpace(fieldDef?.Type) ? FieldType.Input : fieldDef.Type,
                ChangeType = changeLog.ChangeType,
                OriVallue = changeLog.OriValue,
                NewVallue = changeLog.NewValue
            };
        }

        protected override Task BeforeAdd(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            var formDef = GetFromStore<FormDef>(entities.First().FormId)!;
            EnsureAddScope(entities, formDef);
            foreach (var entity in entities)
            {
                _attachmentReferenceService.Apply(entity, null, session);
            }
            if (Context.Action == DataAction.Submit)
            {
                ValidateRequiredFields(entities.First(), formDef);
                entities.ForEach(entity => ResolveSerialNumbers(entity, formDef, null));
            }
            if (!formDef.UsingWorkflow)
            {
                //非流程单据直接生效
                entities.ForEach(entity => { entity.FlowStatus = FlowStatus.Approved; });
            }
            return base.BeforeAdd(entities, session);
        }

        private void EnsureAddScope(IEnumerable<FormData> entities, FormDef formDef)
        {
            if (formDef == null)
            {
                throw new BadRequestException("表单不存在");
            }

            if (!string.IsNullOrWhiteSpace(Context.CorpId) &&
                !string.Equals(Context.CorpId, formDef.CorpId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("不能向其他企业的表单新增数据");
            }

            foreach (var entity in entities)
            {
                if (!string.Equals(entity.FormId, formDef.Id, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(entity.AppId, formDef.AppId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BadRequestException("请求新增数据的应用或表单不一致");
                }

                // FormDataRequest does not carry CorpId. Use the owning form as the authoritative source.
                entity.CorpId = formDef.CorpId;
            }
        }

        public override async Task AddAsync(IEnumerable<FormData> entities)
        {
            await base.AddAsync(entities);
            await SubmitAsync(entities, null, EIMSNext.Service.Entities.CascadeMode.NotSet, null);
        }

        public void Add(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            AddCore(entities, session);
        }

        protected override async Task AfterAdd(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            var outboxPublisher = Resolver.Resolve<IOutboxPublisher>();
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var entity = entities.First();
            var webhookEventId = Guid.NewGuid().ToString("N");
            var webhookPayload = (entity).SerializeToJson();
            MongoTransactionScope.RegisterAfterCommit(() => EnqueueWebhookAsync(outboxPublisher, entity, WebHookTrigger.Data_Created, webhookPayload, webhookEventId));

            await EnqueueFormNotify(messagePublisher, entity, null, FormNotifyTriggerMode.DataAdded);
            await RebuildTimeFieldNotifySchedulesAsync(entity, session);
            await base.AfterAdd(entities, session);
        }

        public override async Task<ReplaceOneResult> ReplaceAsync(FormData entity)
        {
            var old = ScopeCache.Get<FormData>(entity.Id, DataVersion.Old);
            if (old == null && ShouldTriggerFormDataChangeDataflow())
            {
                old = Get(entity.Id);
                if (old != null)
                {
                    ScopeCache.Set(entity.Id, old.DeepClone(), DataVersion.Old);
                }
            }

            var changeFields = old == null
                ? []
                : ExpandoComparer.Compare(old.Data, entity.Data)
                    .Select(x => x.FieldId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var result = await base.ReplaceAsync(entity);
            await SubmitAsync([entity], null, EIMSNext.Service.Entities.CascadeMode.NotSet, null);

            if (ShouldTriggerFormDataChangeDataflow() && changeFields.Count > 0)
            {
                await RunFormDataflowAsync(entity, ApiClient.Flow.EventType.Modified, EIMSNext.Service.Entities.CascadeMode.NotSet, null, changeFields);
            }

            return result;
        }

        public ReplaceOneResult Replace(FormData entity, IClientSessionHandle? session)
        {
            return ReplaceCore(entity, session);
        }

        public object Delete(IEnumerable<string> ids, IClientSessionHandle? session)
        {
            return DeleteCore(FilterBuilder.In(x => x.Id, ids), session);
        }

        protected override object DeleteCore(FilterDefinition<FormData> filter, IClientSessionHandle? session)
        {
            BeforeDelete(filter, session).Wait();

            var targets = FindDeleteTargets(filter, session);
            EnsureCanDeleteTargets(targets);
            var physicalIds = targets
                .Where(x => x.FlowStatus == FlowStatus.Draft && !x.DeleteFlag)
                .Select(x => x.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var physicalIdSet = physicalIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var logicIds = targets
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x) && !physicalIdSet.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _attachmentReferenceService.Release(targets.Where(x => physicalIdSet.Contains(x.Id)), session);

            var logicDeleted = DeleteFormDataByIds(logicIds, physical: false, session);
            if (logicIds.Count > 0)
            {
                CreateAuditLog(DbAction.Delete, null, null, FilterBuilder.In(x => x.Id, logicIds), null, session);
            }

            DeleteStronglyRelatedData(physicalIds, session);
            var physicalDeleted = DeleteFormDataByIds(physicalIds, physical: true, session);
            if (physicalIds.Count > 0)
            {
                DeleteWorkflowInstancesByDataIdsAsync(physicalIds).GetAwaiter().GetResult();
                CreatePhysicalDeleteAuditLog(targets.Where(x => physicalIdSet.Contains(x.Id)), session);
            }
            AfterDelete(filter, session).Wait();

            return new { LogicDeleted = logicDeleted, PhysicalDeleted = physicalDeleted };
        }

        protected override async Task<object> DeleteCoreAsync(FilterDefinition<FormData> filter, IClientSessionHandle? session)
        {
            await BeforeDelete(filter, session);

            var targets = await FindDeleteTargetsAsync(filter, session);
            EnsureCanDeleteTargets(targets);
            var physicalIds = targets
                .Where(x => x.FlowStatus == FlowStatus.Draft && !x.DeleteFlag)
                .Select(x => x.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var physicalIdSet = physicalIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var logicIds = targets
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x) && !physicalIdSet.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _attachmentReferenceService.Release(targets.Where(x => physicalIdSet.Contains(x.Id)), session);

            var logicDeleted = await DeleteFormDataByIdsAsync(logicIds, physical: false, session);
            if (logicIds.Count > 0)
            {
                CreateAuditLog(DbAction.Delete, null, null, FilterBuilder.In(x => x.Id, logicIds), null, session);
            }

            await DeleteStronglyRelatedDataAsync(physicalIds, session);
            var physicalDeleted = await DeleteFormDataByIdsAsync(physicalIds, physical: true, session);
            if (physicalIds.Count > 0)
            {
                await DeleteWorkflowInstancesByDataIdsAsync(physicalIds);
                CreatePhysicalDeleteAuditLog(targets.Where(x => physicalIdSet.Contains(x.Id)), session);
            }
            await AfterDelete(filter, session);

            return new { LogicDeleted = logicDeleted, PhysicalDeleted = physicalDeleted };
        }

        private static void EnsureCanDeleteTargets(IEnumerable<FormData> targets)
        {
            if (targets.Any(x => !x.DeleteFlag && (x.FlowStatus == FlowStatus.Approving || x.FlowStatus == FlowStatus.Suspended)))
            {
                throw new BadRequestException("审批中的数据不允许删除");
            }
        }

        public override Task<object> DeleteAsync(string id)
        {
            return DeleteAsync([id]);
        }

        public override async Task<object> DeleteAsync(IEnumerable<string> ids)
        {
            var idList = ids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var deleting = ShouldTriggerFormDataChangeDataflow() && idList.Count > 0
                ? Find(x => idList.Contains(x.Id)).ToList()
                : [];

            var result = await base.DeleteAsync(idList);

            foreach (var entity in deleting)
            {
                await RunFormDataflowAsync(entity, ApiClient.Flow.EventType.Removed, EIMSNext.Service.Entities.CascadeMode.NotSet, null, null);
            }

            return result;
        }

        public async Task RestoreAsync(IEnumerable<string> ids)
        {
            var idList = ids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (idList.Count == 0) return;

            using var scope = NewTransactionScope();
            var filter = FilterBuilder.And(
                FilterBuilder.In(x => x.Id, idList),
                FilterBuilder.Eq(x => x.DeleteFlag, true));
            var update = UpdateBuilder.Set(x => x.DeleteFlag, false);
            await PatchManyCoreAsync(filter, update, false, scope.SessionHandle);
            scope.CommitTransaction();
        }

        public async Task PurgeAsync(IEnumerable<string> ids)
        {
            var idList = ids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (idList.Count == 0) return;

            using var scope = NewTransactionScope();
            await PurgeCoreAsync(idList, scope.SessionHandle);
            scope.CommitTransaction();
        }

        public override async Task<object> DeleteAsync(DynamicFilter filter)
        {
            var deleting = ShouldTriggerFormDataChangeDataflow()
                ? Repository.Collection.Find(filter.ToFilterDefinition<FormData>()).ToList()
                : [];

            var result = await base.DeleteAsync(filter);

            foreach (var entity in deleting)
            {
                await RunFormDataflowAsync(entity, ApiClient.Flow.EventType.Removed, EIMSNext.Service.Entities.CascadeMode.NotSet, null, null);
            }

            return result;
        }

        protected virtual async Task PurgeCoreAsync(IReadOnlyCollection<string> ids, IClientSessionHandle? session)
        {
            if (ids.Count == 0) return;

            var filter = FilterBuilder.And(
                FilterBuilder.In(x => x.Id, ids),
                FilterBuilder.Eq(x => x.DeleteFlag, true));
            var targets = await FindDeleteTargetsAsync(filter, session);
            var dataIds = targets
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (dataIds.Count == 0) return;

            _attachmentReferenceService.Release(targets, session);

            await DeleteStronglyRelatedDataAsync(dataIds, session);
            await DeleteFormDataByIdsAsync(dataIds, physical: true, session);
            await DeleteWorkflowInstancesByDataIdsAsync(dataIds);
        }

        protected virtual IReadOnlyList<FormData> FindDeleteTargets(FilterDefinition<FormData> filter, IClientSessionHandle? session)
        {
            return session == null
                ? Repository.Collection.Find(filter).ToList()
                : Repository.Collection.Find(session, filter).ToList();
        }

        protected virtual async Task<IReadOnlyList<FormData>> FindDeleteTargetsAsync(FilterDefinition<FormData> filter, IClientSessionHandle? session)
        {
            return session == null
                ? await Repository.Collection.Find(filter).ToListAsync()
                : await Repository.Collection.Find(session, filter).ToListAsync();
        }

        private void CreatePhysicalDeleteAuditLog(IEnumerable<FormData> entities, IClientSessionHandle? session)
        {
            if (!LogAudit) return;

            var list = entities.ToList();
            if (list.Count == 0) return;

            var now = DateTime.UtcNow.ToTimeStampMs();
            var op = Context.Operator;
            var ip = Context.ClientIp;
            var corpId = Context.CorpId;
            var logs = list.Select(x => new AuditLog
            {
                Action = DbAction.PhysicalDelete,
                EntityType = nameof(FormData),
                DataId = x.Id,
                Detail = "物理删除草稿数据",
                OldData = x.SerializeToJson(),
                CreateBy = op,
                UpdateBy = op,
                CreateTime = now,
                UpdateTime = now,
                ClientIp = ip,
                CorpId = string.IsNullOrWhiteSpace(x.CorpId) ? corpId : x.CorpId,
            }).ToList();

            Resolver.GetRepository<AuditLog>().Insert(logs, session);
        }

        protected virtual long DeleteFormDataByIds(IReadOnlyCollection<string> ids, bool physical, IClientSessionHandle? session)
        {
            if (ids.Count == 0) return 0;

            if (physical)
            {
                return Repository.Delete(ids, session).DeletedCount;
            }

            var filter = FilterBuilder.In(x => x.Id, ids);
            var update = UpdateBuilder.Set(Fields.DeleteFlag, true);
            return Repository.UpdateMany(filter, update, session: session).ModifiedCount;
        }

        protected virtual async Task<long> DeleteFormDataByIdsAsync(IReadOnlyCollection<string> ids, bool physical, IClientSessionHandle? session)
        {
            if (ids.Count == 0) return 0;

            if (physical)
            {
                return (await Repository.DeleteAsync(ids, session)).DeletedCount;
            }

            var filter = FilterBuilder.In(x => x.Id, ids);
            var update = UpdateBuilder.Set(Fields.DeleteFlag, true);
            return (await Repository.UpdateManyAsync(filter, update, session: session)).ModifiedCount;
        }

        protected virtual Task<WfResponse?> DeleteWorkflowInstancesByDataIdsAsync(IReadOnlyCollection<string> dataIds)
        {
            return dataIds.Count == 0
                ? Task.FromResult<WfResponse?>(null)
                : _flowClient.DeleteWorkflowInstances(new DeleteWorkflowInstancesRequest { DataIds = dataIds }, Context.AccessToken);
        }

        protected virtual void DeleteStronglyRelatedData(IReadOnlyCollection<string> dataIds, IClientSessionHandle? session)
        {
            if (dataIds.Count == 0) return;

            var taskRepo = Resolver.GetRepository<Wf_Task>();
            taskRepo.Delete(taskRepo.FilterBuilder.In(x => x.DataId, dataIds), session);

            var dataflowScheduleRepo = Resolver.GetRepository<DataflowScheduleItem>();
            dataflowScheduleRepo.Delete(dataflowScheduleRepo.FilterBuilder.In(x => x.DataId, dataIds), session);

            var notifyScheduleRepo = Resolver.GetRepository<FormNotifyScheduleItem>();
            notifyScheduleRepo.Delete(notifyScheduleRepo.FilterBuilder.In(x => x.DataId, dataIds), session);
        }

        protected virtual async Task DeleteStronglyRelatedDataAsync(IReadOnlyCollection<string> dataIds, IClientSessionHandle? session)
        {
            if (dataIds.Count == 0) return;

            var taskRepo = Resolver.GetRepository<Wf_Task>();
            await taskRepo.DeleteAsync(taskRepo.FilterBuilder.In(x => x.DataId, dataIds), session);

            var dataflowScheduleRepo = Resolver.GetRepository<DataflowScheduleItem>();
            await dataflowScheduleRepo.DeleteAsync(dataflowScheduleRepo.FilterBuilder.In(x => x.DataId, dataIds), session);

            var notifyScheduleRepo = Resolver.GetRepository<FormNotifyScheduleItem>();
            await notifyScheduleRepo.DeleteAsync(notifyScheduleRepo.FilterBuilder.In(x => x.DataId, dataIds), session);
        }

        protected override async Task AfterReplace(FormData entity, IClientSessionHandle? session)
        {
            var outboxPublisher = Resolver.Resolve<IOutboxPublisher>();
            var messagePublisher = Resolver.Resolve<IMessagePublisher>();
            var old = ScopeCache.Get<FormData>(entity.Id, DataVersion.Old);
            var oriValue = new ExpandoObject();
            IList<ExpandoChangeLog> changeLogs = [];
            if (old != null)
            {
                changeLogs = ExpandoComparer.Compare(old.Data, entity.Data);
                changeLogs.ForEach(x => oriValue.TryAdd(x.FieldId, x.OriValue));
                CreateFormDataChangeLog(entity, changeLogs, session);
            }

            var formExp = entity.SerializeToJson().DeserializeFromJson<ExpandoObject>()!;
            formExp.TryAdd("oridata", oriValue);
            var webhookEventId = Guid.NewGuid().ToString("N");
            MongoTransactionScope.RegisterAfterCommit(() => EnqueueWebhookAsync(outboxPublisher, entity, WebHookTrigger.Data_Updated, formExp.SerializeToJson(), webhookEventId));

            await EnqueueFormNotify(messagePublisher, entity, old, FormNotifyTriggerMode.DataChanged);
            await RebuildTimeFieldNotifySchedulesAsync(entity, session);

            await base.AfterReplace(entity, session);
        }

        private void CreateFormDataChangeLog(FormData entity, IList<ExpandoChangeLog> changeLogs, IClientSessionHandle? session)
        {
            if (changeLogs.Count == 0) return;

            var formDef = GetFromStore<FormDef>(entity.FormId);
            var fieldLookup = BuildFieldLookup(formDef);
            var content = changeLogs.Select(x => ToDataChangeContent(x, fieldLookup)).ToList();
            if (content.Count == 0) return;

            var now = DateTime.UtcNow.ToTimeStampMs();
            Resolver.GetRepository<FormDataChangeLog>().Insert(new FormDataChangeLog
            {
                CorpId = entity.CorpId ?? Context.CorpId,
                AppId = entity.AppId,
                FormId = entity.FormId,
                DataId = entity.Id,
                Operator = Context.Operator,
                OperateTime = now,
                Content = content,
                CreateBy = Context.Operator,
                CreateTime = now,
                UpdateBy = Context.Operator,
                UpdateTime = now
            }, session);
        }

        protected override Task BeforeReplace(FormData entity, IClientSessionHandle? session)
        {
            var old = ScopeCache.Get<FormData>(entity.Id, DataVersion.Old) ?? GetFromStore<FormData>(entity.Id, DataVersion.Old);
            var formDef = GetFromStore<FormDef>(entity.FormId)!;
            EnsureCanEdit(entity, formDef);
            _attachmentReferenceService.Apply(entity, old, session);
            if (Context.Action == DataAction.Submit)
            {
                ValidateRequiredFields(entity, formDef);
                ResolveSerialNumbers(entity, formDef, old);
            }

            return base.BeforeReplace(entity, session);
        }

        protected static void EnsureCanEdit(FormData entity, FormDef formDef)
        {
            if (!formDef.UsingWorkflow)
            {
                return;
            }

            if (entity.FlowStatus is FlowStatus.Approving or FlowStatus.Approved or FlowStatus.Suspended or FlowStatus.Discarded)
            {
                throw new BadRequestException("审批中的或已完成的流程数据不允许修改");
            }
        }

        public async Task SubmitAsync(IEnumerable<FormData> entities, IClientSessionHandle? session, EIMSNext.Service.Entities.CascadeMode cascade, string? eventIds)
        {
            var entity = entities.First();

            if (Context.Action == EIMSNext.Core.Abstractions.DataAction.Submit)
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
                    await RunFormDataflowAsync(entity, ApiClient.Flow.EventType.Submitted, cascade, eventIds, null);
                }
            }
        }

        private bool ShouldTriggerFormDataChangeDataflow()
        {
            return Context.Action == DataAction.None || Context.Action == DataAction.Save;
        }

        private async Task RunFormDataflowAsync(
            FormData entity,
            ApiClient.Flow.EventType eventType,
            EIMSNext.Service.Entities.CascadeMode cascade,
            string? eventIds,
            IEnumerable<string>? changeFields)
        {
            if (cascade == EIMSNext.Service.Entities.CascadeMode.Never)
            {
                return;
            }

            var dfResp = await _flowClient.RunDataflow(new DfRunRequest
            {
                DataId = entity.Id,
                EventSource = ApiClient.Flow.EventSourceType.Form,
                EventType = eventType,
                DfCascade = (ApiClient.Flow.CascadeMode)cascade,
                EventIds = eventIds,
                ChangeFields = changeFields?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            }, Context.AccessToken);
            if (dfResp != null && !string.IsNullOrEmpty(dfResp.Error))
            {
                throw new UnLogException(dfResp.Error);
            }
        }

        /// <summary>
        /// 解析表单数据中的所有 serialno 字段:
        /// - 当前值非空时保留,允许外部系统/API 预写流水号
        /// - 当前值为空但旧值非空时保留旧值
        /// - 当前值和旧值都为空时按规则生成
        /// </summary>
        private void ResolveSerialNumbers(FormData entity, FormDef formDef, FormData? oldEntity)
        {
            if (formDef?.Content == null) return;
            entity.Data ??= new ExpandoObject();
            var layout = formDef.Content.Layout;
            if (string.IsNullOrWhiteSpace(layout)) return;

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(layout);
            }
            catch (JsonException)
            {
                throw new BadRequestException("表单布局配置无效，无法生成流水号");
            }

            var dataDict = (IDictionary<string, object?>)entity.Data!;
            var oldDataDict = oldEntity?.Data as IDictionary<string, object?>;
            WalkSerialNoRules(doc.RootElement, (rule) =>
            {
                if (!rule.TryGetProperty("field", out var fieldProp) || fieldProp.ValueKind != JsonValueKind.String) return;
                var field = fieldProp.GetString();
                if (string.IsNullOrEmpty(field)) return;

                if (dataDict.TryGetValue(field, out var currentValue)
                    && !string.IsNullOrWhiteSpace(currentValue?.ToString()))
                {
                    return;
                }

                if (oldDataDict != null
                    && oldDataDict.TryGetValue(field, out var oldValue)
                    && !string.IsNullOrWhiteSpace(oldValue?.ToString()))
                {
                    dataDict[field] = oldValue;
                    return;
                }

                if (!rule.TryGetProperty("props", out var propsEl) || propsEl.ValueKind != JsonValueKind.Object) return;
                if (!propsEl.TryGetProperty("segments", out var segmentsEl) || segmentsEl.ValueKind != JsonValueKind.Array) return;

                var sb = new StringBuilder();
                foreach (var seg in segmentsEl.EnumerateArray())
                {
                    AppendSegment(seg, sb, entity, dataDict, field);
                }
                dataDict[field] = sb.ToString();
            });

            doc.Dispose();
        }

        private static void ValidateRequiredFields(FormData entity, FormDef formDef)
        {
            if (formDef.Content?.Items == null || formDef.Content.Items.Count == 0)
            {
                return;
            }

            using var document = JsonDocument.Parse(entity.Data.SerializeToJson());
            foreach (var field in formDef.Content.Items)
            {
                ValidateField(field, document.RootElement, field.Field);
            }

            static void ValidateField(FieldDef field, JsonElement parent, string path)
            {
                var value = TryGetProperty(parent, field.Field, out var property)
                    ? property
                    : default;

                if ((field.Required || field.Props?.Required == true) && IsEmpty(value))
                {
                    throw new BadRequestException($"字段 [{path}] 不能为空");
                }

                if (string.Equals(field.Type, FieldType.TimeStamp, StringComparison.OrdinalIgnoreCase)
                    && !IsEmpty(value)
                    && !IsTimestamp(value))
                {
                    throw new BadRequestException($"字段 [{path}] 必须为毫秒时间戳");
                }

                if (field.Columns == null || value.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                var rowIndex = 0;
                foreach (var row in value.EnumerateArray())
                {
                    foreach (var column in field.Columns)
                    {
                        ValidateField(column, row, $"{path}[{rowIndex}].{column.Field}");
                    }

                    rowIndex++;
                }
            }

            static bool TryGetProperty(JsonElement parent, string name, out JsonElement value)
            {
                if (parent.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in parent.EnumerateObject())
                    {
                        if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            value = property.Value;
                            return true;
                        }
                    }
                }

                value = default;
                return false;
            }

            static bool IsEmpty(JsonElement value)
            {
                return value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())
                    || value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0;
            }

            static bool IsTimestamp(JsonElement value)
            {
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _)
                    || value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out _);
            }
        }

        private void AppendSegment(JsonElement seg, StringBuilder sb, FormData entity, IDictionary<string, object?> dataDict, string serialNoField)
        {
            if (!seg.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String) return;
            var type = typeEl.GetString();
            switch (type)
            {
                case "fixed":
                    {
                        var v = seg.TryGetProperty("value", out var ve) && ve.ValueKind == JsonValueKind.String ? ve.GetString() : null;
                        sb.Append(v ?? string.Empty);
                        break;
                    }
                case "date":
                    {
                        var fmt = seg.TryGetProperty("format", out var fe) && fe.ValueKind == JsonValueKind.String
                            ? fe.GetString()
                            : "yyyyMMdd";
                        sb.Append(DateTime.UtcNow.ToString(NormalizeDateFormat(fmt), CultureInfo.InvariantCulture));
                        break;
                    }
                case "field":
                    {
                        if (seg.TryGetProperty("field", out var fe) && fe.ValueKind == JsonValueKind.String)
                        {
                            var refField = fe.GetString();
                            if (!string.IsNullOrEmpty(refField) && dataDict.TryGetValue(refField, out var fv) && fv != null)
                            {
                                sb.Append(fv.ToString() ?? string.Empty);
                            }
                        }
                        break;
                    }
                case "counter":
                    {
                        var digits = seg.TryGetProperty("digits", out var de) && de.ValueKind == JsonValueKind.Number ? de.GetInt32() : 5;
                        var padZero = !(seg.TryGetProperty("padZero", out var pe) && pe.ValueKind == JsonValueKind.False);
                        var cycle = SerialNoResetCycle.Never;
                        if (seg.TryGetProperty("reset", out var re) && re.ValueKind == JsonValueKind.String)
                        {
                            cycle = re.GetString() switch
                            {
                                "day" => SerialNoResetCycle.Day,
                                "month" => SerialNoResetCycle.Month,
                                "year" => SerialNoResetCycle.Year,
                                _ => SerialNoResetCycle.Never
                            };
                        }
                        var seq = _serialNoSvc.NextFormSerialNo(
                            entity.CorpId ?? string.Empty,
                            entity.AppId,
                            entity.FormId,
                            serialNoField,
                            cycle);
                        sb.Append(FormatCounter(seq, digits, padZero));
                        break;
                    }
            }
        }

        private static string FormatCounter(int seq, int digits, bool padZero)
        {
            if (!padZero || digits <= 0) return seq.ToString(CultureInfo.InvariantCulture);
            return seq.ToString("D" + Math.Min(digits, 10).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string NormalizeDateFormat(string? format)
        {
            if (string.IsNullOrWhiteSpace(format)) return "yyyyMMdd";
            return format.All(c => c is 'y' or 'M' or 'd' or '-' or '_' or '/' or '.') ? format : "yyyyMMdd";
        }

        private static void WalkSerialNoRules(JsonElement node, Action<JsonElement> visit)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                    && t.GetString() == FieldType.SerialNo)
                {
                    visit(node);
                    // 不递归 children,避免 tableform 内嵌的同名子规则被误处理
                    return;
                }
                foreach (var prop in node.EnumerateObject())
                {
                    if (prop.Name == "children" && prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var child in prop.Value.EnumerateArray())
                            WalkSerialNoRules(child, visit);
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        WalkSerialNoRules(prop.Value, visit);
                    }
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                    WalkSerialNoRules(item, visit);
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
                    Id = id!,
                    Label = label!,
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

        private Task EnqueueFormNotify(IMessagePublisher publisher, FormData newData, FormData? oldData, FormNotifyTriggerMode triggerMode)
        {
            return publisher.PublishAsync(new NotifyDispatchTaskArgs
            {
                CorpId = Context.CorpId,
                MessageType = MessageType.FormNotify,
                AppId = newData.AppId,
                FormId = newData.FormId,
                TargetType = NotifyTargetType.Form,
                DataId = newData.Id,
                FormTriggerMode = triggerMode,
                Operator = Context.Operator,
                EventStamp = newData.UpdateTime ?? newData.CreateTime,
                NewData = newData.SerializeToJson().DeserializeFromJson<FormData>()!,
                OldData = oldData?.SerializeToJson().DeserializeFromJson<FormData>()
            });
        }

        private static Task EnqueueWebhookAsync(IOutboxPublisher publisher, FormData entity, WebHookTrigger trigger, string payloadJson, string eventId)
        {
            return publisher.EnqueueAsync(new WebhookTaskArgs
            {
                CorpId = entity.CorpId ?? string.Empty,
                AppId = entity.AppId,
                FormId = entity.FormId,
                Trigger = trigger,
                PayloadJson = payloadJson,
                DataId = entity.Id,
                EventId = eventId
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
                x.TargetType == NotifyTargetType.Form &&
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
                    TargetType = NotifyTargetType.Form,
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

                var adjustedAnchor = FormNotifyRuntime.ResolveAdjustedAnchor(notify, anchorTime.Value) ?? anchorTime.Value;
                var nextTriggerTime = FormNotifyScheduleCalculator.CalculateNextTriggerTime(notify, adjustedAnchor);
                if (!nextTriggerTime.HasValue)
                {
                    continue;
                }

                await scheduleRepo.InsertAsync(new FormNotifyScheduleItem
                {
                    NotifyId = notify.Id,
                    DataId = entity.Id,
                    AppId = notify.AppId,
                    FormId = notify.FormId,
                    TargetType = NotifyTargetType.Form,
                    CorpId = notify.CorpId,
                    TriggerMode = FormNotifyTriggerMode.TimeFieldScheduled,
                    ScheduleVersion = notify.ScheduleVersion,
                    TriggerTime = nextTriggerTime.Value,
                    AnchorTime = adjustedAnchor,
                    TimeField = notify.TimeField
                }, session);
            }
        }
    }
}
