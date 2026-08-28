using EIMSNext.ApiClient.Flow;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace EIMSNext.Service
{
    public class FormDefService : EntityServiceBase<FormDef>, IFormDefService
    {
        private FlowApiClient _flowClient;
        public FormDefService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        protected override async Task AfterAdd(IEnumerable<FormDef> entities, IClientSessionHandle? session)
        {
            await base.AfterAdd(entities, session);
            var appRepo = Resolver.GetRepository<AppDef>();
            var app = appRepo.Get(entities.First().AppId, session)!;
            var maxIndex = app.AppMenus.Count == 0 ? 0 : app.AppMenus.Max(x => x.SortIndex);
            entities.ForEach(e =>
            {
                maxIndex = maxIndex + 100;
                app.AppMenus.Add(new AppMenu { MenuId = e.Id, Icon = "", IconColor = "", MenuType = FormType.Form, Title = e.Name, SortIndex = maxIndex });
            });
            appRepo.Replace(app, session);

            return;
        }

        protected override Task BeforeAdd(IEnumerable<FormDef> entities, IClientSessionHandle? session)
        {
            foreach (var entity in entities)
            {
                entity.Content.FieldChangeLogs = [];
                NormalizeFieldMetadata(entity);
                ValidateFieldIds(entity);
            }
            return base.BeforeAdd(entities, session);
        }

        protected override Task BeforeReplace(FormDef entity, IClientSessionHandle? session)
        {
            var old = ScopeCache.Get<FormDef>(entity.Id, Cache.DataVersion.Old)
                ?? GetFromStore<FormDef>(entity.Id, Cache.DataVersion.Old);
            ReconcileFieldChangeLogs(old?.Content, entity.Content, Context.Operator, DateTime.UtcNow.ToTimeStampMs());
            NormalizeFieldMetadata(entity);
            ValidateFieldIds(entity);
            return base.BeforeReplace(entity, session);
        }

        public async Task PurgeFieldChangeLogsAsync(string formId, IReadOnlyCollection<string> fieldIds, bool clearAll)
        {
            var normalizedIds = fieldIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!clearAll && normalizedIds.Count == 0)
            {
                throw new BadRequestException("请选择要彻底删除的字段");
            }

            var filter = FilterBuilder.And(
                FilterBuilder.Eq(x => x.Id, formId),
                FilterBuilder.Eq(x => x.CorpId, Context.CorpId),
                FilterBuilder.Eq(x => x.DeleteFlag, false));
            var update = clearAll
                ? UpdateBuilder.Set(x => x.Content.FieldChangeLogs, new List<FieldChangeLog>())
                : UpdateBuilder.PullFilter(x => x.Content.FieldChangeLogs, x => normalizedIds.Contains(x.FieldId));

            using var scope = NewTransactionScope();
            await PatchManyCoreAsync(filter, update, false, scope.SessionHandle);
            scope.CommitTransaction();
        }

        internal static void ReconcileFieldChangeLogs(
            FormContent? oldContent,
            FormContent newContent,
            Operator? deletedBy,
            long deletedTime)
        {
            var oldFields = FlattenFields(oldContent?.Items);
            var newFields = FlattenFields(newContent.Items);
            var activeIds = newFields.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var logs = (oldContent?.FieldChangeLogs ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x.FieldId) && !activeIds.Contains(x.FieldId))
                .GroupBy(x => x.FieldId, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.OrderByDescending(log => log.DeletedTime).First())
                .ToDictionary(x => x.FieldId, StringComparer.OrdinalIgnoreCase);

            foreach (var oldField in oldFields.Values)
            {
                if (activeIds.Contains(oldField.FieldId) || logs.ContainsKey(oldField.FieldId))
                {
                    continue;
                }

                logs[oldField.FieldId] = new FieldChangeLog
                {
                    FieldId = oldField.FieldId,
                    FieldType = oldField.FieldType,
                    FieldLabel = oldField.FieldLabel,
                    DeletedBy = deletedBy ?? Operator.Empty,
                    DeletedTime = deletedTime
                };
            }

            newContent.FieldChangeLogs = logs.Values
                .OrderByDescending(x => x.DeletedTime)
                .ThenBy(x => x.FieldId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static Dictionary<string, FieldChangeSnapshot> FlattenFields(IList<FieldDef>? fields)
        {
            var result = new Dictionary<string, FieldChangeSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields ?? [])
            {
                if (string.IsNullOrWhiteSpace(field.Field))
                {
                    continue;
                }

                result.TryAdd(field.Field, new FieldChangeSnapshot(field.Field, field.Type, field.Title));
                foreach (var column in field.Columns ?? [])
                {
                    if (string.IsNullOrWhiteSpace(column.Field))
                    {
                        continue;
                    }

                    var fieldId = $"{field.Field}>{column.Field}";
                    var fieldLabel = $"{field.Title}.{column.Title}";
                    result.TryAdd(fieldId, new FieldChangeSnapshot(fieldId, column.Type, fieldLabel));
                }
            }

            return result;
        }

        internal sealed record FieldChangeSnapshot(string FieldId, string FieldType, string FieldLabel);

        private static void NormalizeFieldMetadata(FormDef formDef)
        {
            if (formDef?.Content?.Items == null)
            {
                return;
            }

            foreach (var field in formDef.Content.Items)
            {
                NormalizeField(field);
            }

            static void NormalizeField(FieldDef field)
            {
                if (field.Props?.Required == true)
                {
                    field.Required = true;
                }

                if (field.Columns == null)
                {
                    return;
                }

                foreach (var column in field.Columns)
                {
                    NormalizeField(column);
                }
            }
        }

        /// <summary>
        /// 校验 FormDef 中所有字段 ID 符合 <see cref="FieldIdRules"/>。
        /// 失败时抛 <see cref="BadRequestException"/>，由 controller 统一转换为 400。
        /// </summary>
        private static void ValidateFieldIds(FormDef formDef)
        {
            if (formDef?.Content?.Items == null)
            {
                return;
            }

            var fieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in formDef.Content.Items)
            {
                var err = FieldIdRules.ValidateFieldId(field.Field);
                if (!string.IsNullOrEmpty(err))
                {
                    throw new BadRequestException($"表单 [{formDef.Name}] 字段 ID 非法: {err}");
                }

                if (!fieldIds.Add(field.Field))
                {
                    throw new BadRequestException($"表单 [{formDef.Name}] 字段 ID 重复: {field.Field}");
                }

                if (field.Columns != null)
                {
                    foreach (var col in field.Columns)
                    {
                        var subErr = FieldIdRules.ValidateSubFieldId(col.Field);
                        if (!string.IsNullOrEmpty(subErr))
                        {
                            throw new BadRequestException($"表单 [{formDef.Name}] 子表列 ID 非法: {subErr}");
                        }

                        if (!fieldIds.Add(col.Field))
                        {
                            throw new BadRequestException($"表单 [{formDef.Name}] 字段 ID 重复: {col.Field}");
                        }
                    }
                }
            }
        }

        protected override async Task AfterReplace(FormDef entity, IClientSessionHandle? session)
        {
            await base.AfterReplace(entity, session);
            var appRepo = Resolver.GetRepository<AppDef>();
            var app = appRepo.Get(entity.AppId, session)!;

            var menu = AppMenuHelper.FindMenu(app.AppMenus, entity.Id);
            if (menu != null)
            {
                menu.Title = entity.Name;
                appRepo.Replace(app, session);
            }
        }
        protected override async Task AfterUpdate(FilterDefinition<FormDef> filter, UpdateDefinition<FormDef> update, bool upsert, IClientSessionHandle? session)
        {
            await base.AfterUpdate(filter, update, upsert, session);
            var updated = Context.ScopeCache.GetAll<FormDef>(Cache.DataVersion.New);
            if (!updated.Any())
            {
                updated = await Collection.Find(filter).ToListAsync();
            }
            if (updated.Any())
            {
                var appRepo = Resolver.GetRepository<AppDef>();
                var app = appRepo.Get(updated.First().AppId, session)!;

                updated.ForEach(e =>
                {
                    var menu = AppMenuHelper.FindMenu(app.AppMenus, e.Id);
                    if (menu != null) menu.Title = e.Name;
                });
                appRepo.Replace(app, session);
            }
        }

        protected override async Task AfterDelete(FilterDefinition<FormDef> filter, IClientSessionHandle? session)
        {
            await base.AfterDelete(filter, session);
            // 找到被删除的 FormDef 实体
            var deletedForms = Repository.Find(new MongoFindOptions<FormDef> { Filter = filter }, session).ToList();
            if (deletedForms.Count == 0)
                return;

            var appRepo = Resolver.GetRepository<AppDef>();

            // 按 AppId 分组，批量处理
            var appIds = deletedForms.Select(f => f.AppId).Distinct();
            foreach (var appId in appIds)
            {
                var app = appRepo.Get(appId, session);
                if (app == null) continue;

                var removedCount = 0;
                foreach (var form in deletedForms.Where(x => x.AppId == appId))
                {
                    if (AppMenuHelper.RemoveMenu(app.AppMenus, form.Id))
                    {
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    AppMenuHelper.Normalize(app.AppMenus);
                    appRepo.Replace(app, session);
                }
            }

            var formIds = deletedForms.Select(x => x.Id);
            //更新所有相关数据为已删除
            var formDataRepo = Resolver.GetRepository<FormData>();
            await formDataRepo.UpdateManyAsync(formDataRepo.FilterBuilder.And(formDataRepo.FilterBuilder.Eq(x => x.DeleteFlag, false), formDataRepo.FilterBuilder.In(x => x.FormId, formIds)), formDataRepo.UpdateBuilder.Set(x => x.DeleteFlag, true), session: session);

            var flowFormIds = deletedForms.Where(x => x.UsingWorkflow).Select(x => x.Id);
            if (flowFormIds.Any())
            {
                var flowFormIdList = flowFormIds.Distinct().ToList();
                //删除所有待办
                var taskRepo = Resolver.GetRepository<Wf_Task>();
                await taskRepo.DeleteAsync(taskRepo.FilterBuilder.In(x => x.FormId, flowFormIdList), session);

            }

            var corpIds = deletedForms.Select(x => x.CorpId).Distinct().ToList();

            // 表单删除后，所有直接引用和嵌入引用都必须失效，避免孤儿配置继续被读取。
            var printRepo = Resolver.GetRepository<PrintDef>();
            await printRepo.UpdateManyAsync(
                printRepo.FilterBuilder.And(
                    printRepo.FilterBuilder.Eq(x => x.DeleteFlag, false),
                    printRepo.FilterBuilder.In(x => x.FormId, formIds)),
                printRepo.UpdateBuilder.Set(x => x.DeleteFlag, true),
                session: session);

            var bindingRepo = Resolver.GetRepository<CrossBinding>();
            await bindingRepo.UpdateManyAsync(
                bindingRepo.FilterBuilder.And(
                    bindingRepo.FilterBuilder.Eq(x => x.DeleteFlag, false),
                    bindingRepo.FilterBuilder.In(x => x.SourceFormId, formIds)),
                bindingRepo.UpdateBuilder.Set(x => x.DeleteFlag, true),
                session: session);

            var permissionGroupRepo = Resolver.GetRepository<FormDataPermissionGroup>();
            await permissionGroupRepo.UpdateManyAsync(
                permissionGroupRepo.FilterBuilder.And(
                    permissionGroupRepo.FilterBuilder.Eq(x => x.DeleteFlag, false),
                    permissionGroupRepo.FilterBuilder.In(x => x.FormId, formIds)),
                permissionGroupRepo.UpdateBuilder.Set(x => x.DeleteFlag, true),
                session: session);

            var itemRepo = Resolver.GetRepository<DashboardItemDef>();
            var embeddedReferenceFilters = formIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => itemRepo.FilterBuilder.Regex(
                    x => x.Details,
                    new BsonRegularExpression(Regex.Escape(id), "i")))
                .ToList();
            if (embeddedReferenceFilters.Count > 0)
            {
                await itemRepo.UpdateManyAsync(
                    itemRepo.FilterBuilder.And(
                        itemRepo.FilterBuilder.In(x => x.CorpId, corpIds),
                        itemRepo.FilterBuilder.Eq(x => x.DeleteFlag, false),
                        itemRepo.FilterBuilder.Or(embeddedReferenceFilters)),
                    itemRepo.UpdateBuilder.Set(x => x.DeleteFlag, true),
                    session: session);
            }

        }

        public override async Task<object> DeleteAsync(string id)
        {
            var flowFormIds = GetWorkflowFormIds(FilterBuilder.Eq(x => x.Id, id));
            var result = await base.DeleteAsync(id);
            await ScheduleFlowDefinitionsCleanupAsync(flowFormIds);
            return result;
        }

        public override async Task<object> DeleteAsync(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var flowFormIds = GetWorkflowFormIds(FilterBuilder.In(x => x.Id, idList));
            var result = await base.DeleteAsync(idList);
            await ScheduleFlowDefinitionsCleanupAsync(flowFormIds);
            return result;
        }

        public override async Task<object> DeleteAsync(DynamicFilter filter)
        {
            var mongoFilter = filter.ToFilterDefinition<FormDef>();
            var flowFormIds = GetWorkflowFormIds(mongoFilter);
            var result = await base.DeleteAsync(filter);
            await ScheduleFlowDefinitionsCleanupAsync(flowFormIds);
            return result;
        }

        private List<string> GetWorkflowFormIds(FilterDefinition<FormDef> filter)
        {
            return Repository.Collection.Find(filter)
                .ToList()
                .Where(x => x.UsingWorkflow)
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private Task ScheduleFlowDefinitionsCleanupAsync(IReadOnlyCollection<string> formIds)
        {
            if (formIds.Count == 0)
            {
                return Task.CompletedTask;
            }

            if (MongoTransactionScope.IsInTransaction)
            {
                MongoTransactionScope.RegisterAfterCommit(() => DeleteFlowDefinitionsAfterCommitAsync(formIds));
                return Task.CompletedTask;
            }

            return DeleteFlowDefinitionsAfterCommitAsync(formIds);
        }

        private async Task DeleteFlowDefinitionsAfterCommitAsync(IReadOnlyCollection<string> formIds)
        {
            if (formIds.Count == 0)
            {
                return;
            }

            try
            {
                var response = await _flowClient.DeleteDef(new DeleteRequest
                {
                    DeleteDef = true,
                    FormIds = formIds.ToList()
                }, Context.AccessToken);

                if (!string.IsNullOrWhiteSpace(response?.Error))
                {
                    Logger.LogError(
                        "Flow definition cleanup returned an error after form deletion. CorpId={CorpId}, FormIds={FormIds}, Error={Error}",
                        Context.CorpId,
                        string.Join(',', formIds),
                        response.Error);

                    // TODO: 将来通过系统消息通知系统维保人员处理流程定义清理失败。
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Failed to delete Flow definitions after form deletion. CorpId={CorpId}, FormIds={FormIds}",
                    Context.CorpId,
                    string.Join(',', formIds));

                // TODO: 将来通过系统消息通知系统维保人员处理流程定义清理失败。
            }
        }

    }
}
