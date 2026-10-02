using System.Linq.Expressions;
using EIMSNext.ApiClient.Flow;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Query;
using EIMSNext.Core;

namespace EIMSNext.Service
{
    public class FormDefService : EntityServiceBase<FormDef>, IFormDefService
    {
        private FlowApiClient _flowClient;
        public FormDefService(IResolver resolver) : base(resolver)
        {
            _flowClient = resolver.Resolve<FlowApiClient>();
        }

        protected override async Task AfterAdd(IEnumerable<FormDef> entities)
        {
            await base.AfterAdd(entities);
            var appRepo = Resolver.GetRepository<AppDef>();
            var app = appRepo.Get(entities.First().AppId)!;
            var maxIndex = app.AppMenus.Count == 0 ? 0 : app.AppMenus.Max(x => x.SortIndex);
            entities.ForEach(e =>
            {
                maxIndex = maxIndex + 100;
                app.AppMenus.Add(new AppMenu { MenuId = e.Id, Icon = "", IconColor = "", MenuType = FormType.Form, Title = e.Name, SortIndex = maxIndex });
            });
            appRepo.Replace(app);

            return;
        }

        protected override Task BeforeAdd(IEnumerable<FormDef> entities)
        {
            foreach (var entity in entities)
            {
                entity.Content.FieldChangeLogs = [];
                NormalizeFieldMetadata(entity);
                ValidateFieldIds(entity);
            }
            return base.BeforeAdd(entities);
        }

        protected override Task BeforeReplace(FormDef entity)
        {
            var old = ScopeCache.Get<FormDef>(entity.Id, Cache.DataVersion.Old)
                ?? GetFromStore<FormDef>(entity.Id, Cache.DataVersion.Old);
            ReconcileFieldChangeLogs(old?.Content, entity.Content, Context.Operator, DateTime.UtcNow.ToTimeStampMs());
            NormalizeFieldMetadata(entity);
            ValidateFieldIds(entity);
            return base.BeforeReplace(entity);
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

            await ExecuteWithTransactionRetryAsync(async () =>
            {
                if (clearAll)
                {
                    // 清空全部字段变更日志。
                    await PatchManyCoreAsync(
                        x => x.Id == formId && x.CorpId == Context.CorpId && !x.DeleteFlag,
                        setters => setters.SetProperty(x => x.Content.FieldChangeLogs, new List<FieldChangeLog>()))
                        .ConfigureAwait(false);
                }
                else
                {
                    // 仅移除指定的字段变更日志条目。EF Core 无法对 jsonb 内嵌集合做服务端 PullFilter，
                    // 加载实体后在内存中过滤再整体替换。
                    var target = Repository.Queryable
                        .FirstOrDefault(x => x.Id == formId && x.CorpId == Context.CorpId && !x.DeleteFlag);
                    if (target?.Content?.FieldChangeLogs is { Count: > 0 } logs)
                    {
                        var remaining = logs.Where(x => !normalizedIds.Contains(x.FieldId)).ToList();
                        await PatchManyCoreAsync(
                            x => x.Id == formId && x.CorpId == Context.CorpId && !x.DeleteFlag,
                            setters => setters.SetProperty(x => x.Content.FieldChangeLogs, remaining))
                            .ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
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

        protected override async Task AfterReplace(FormDef entity)
        {
            await base.AfterReplace(entity);
            var appRepo = Resolver.GetRepository<AppDef>();
            var app = appRepo.Get(entity.AppId)!;

            var menu = AppMenuHelper.FindMenu(app.AppMenus, entity.Id);
            if (menu != null)
            {
                menu.Title = entity.Name;
                appRepo.Replace(app);
            }
        }

        protected override async Task AfterUpdate(
            Expression<Func<FormDef, bool>> filter,
            Action<UpdateSettersBuilder<FormDef>> setters)
        {
            await base.AfterUpdate(filter, setters);
            var updated = Context.ScopeCache.GetAll<FormDef>(Cache.DataVersion.New);
            if (!updated.Any())
            {
                updated = await FindCoreAsync(filter).ConfigureAwait(false);
            }
            if (updated.Any())
            {
                var appRepo = Resolver.GetRepository<AppDef>();
                var app = (await appRepo.GetAsync(updated.First().AppId).ConfigureAwait(false))!;

                updated.ForEach(e =>
                {
                    var menu = AppMenuHelper.FindMenu(app.AppMenus, e.Id);
                    if (menu != null) menu.Title = e.Name;
                });
                await appRepo.ReplaceAsync(app).ConfigureAwait(false);
            }
        }

        protected override async Task AfterDelete(Expression<Func<FormDef, bool>> filter)
        {
            await base.AfterDelete(filter);
            // 找到被删除的 FormDef 实体
            // 注意：LogicDelete 在 AfterDelete 之前已经把 DeleteFlag 置 true，而模型层挂了全局
            // `!DeleteFlag` 过滤器，不显式忽略就会读到空集合，下面的菜单清理、关联 FormData 逻辑删除、
            // Wf_Task/PrintDef/CrossBinding/权限组/Dashboard 引用清理会被整条静默跳过。
            var deletedForms = Repository.Queryable
                .IgnoreQueryFilters()
                .Where(filter)
                .ToList();
            if (deletedForms.Count == 0)
                return;

            var appRepo = Resolver.GetRepository<AppDef>();

            // 按 AppId 分组，批量处理
            var appIds = deletedForms.Select(f => f.AppId).Distinct();
            foreach (var appId in appIds)
            {
                var app = appRepo.Get(appId);
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
                    appRepo.Replace(app);
                }
            }

            var formIds = deletedForms.Select(x => x.Id).ToList();
            //更新所有相关数据为已删除
            var formDataRepo = Resolver.GetRepository<FormData>();
            await formDataRepo.UpdateManyAsync(
                x => !x.DeleteFlag && formIds.Contains(x.FormId),
                setters => setters.SetProperty(x => x.DeleteFlag, true));

            var flowFormIds = deletedForms.Where(x => x.UsingWorkflow).Select(x => x.Id);
            if (flowFormIds.Any())
            {
                var flowFormIdList = flowFormIds.Distinct().ToList();
                //删除所有待办
                var taskRepo = Resolver.GetRepository<Wf_Task>();
                await taskRepo.DeleteManyAsync(x => flowFormIdList.Contains(x.FormId));
            }

            var corpIds = deletedForms.Select(x => x.CorpId).Distinct().ToList();

            // 表单删除后，所有直接引用和嵌入引用都必须失效，避免孤儿配置继续被读取。
            var printRepo = Resolver.GetRepository<PrintDef>();
            await printRepo.UpdateManyAsync(
                x => !x.DeleteFlag && formIds.Contains(x.FormId),
                setters => setters.SetProperty(x => x.DeleteFlag, true));

            var bindingRepo = Resolver.GetRepository<CrossBinding>();
            await bindingRepo.UpdateManyAsync(
                x => !x.DeleteFlag && formIds.Contains(x.SourceFormId),
                setters => setters.SetProperty(x => x.DeleteFlag, true));

            var permissionGroupRepo = Resolver.GetRepository<FormDataPermissionGroup>();
            await permissionGroupRepo.UpdateManyAsync(
                x => !x.DeleteFlag && formIds.Contains(x.FormId),
                setters => setters.SetProperty(x => x.DeleteFlag, true));

            // DashboardItemDef.Details 内嵌引用了表单 ID。
            var itemRepo = Resolver.GetRepository<DashboardItemDef>();
            var detailsCandidates = itemRepo.Queryable
                .Where(x => corpIds.Contains(x.CorpId) && !x.DeleteFlag)
                .ToList()
                .Where(x => ContainsAnyFormReference(x.Details, formIds))
                .Select(x => x.Id)
                .ToList();
            if (detailsCandidates.Count > 0)
            {
                await itemRepo.UpdateManyAsync(
                    x => detailsCandidates.Contains(x.Id),
                    setters => setters.SetProperty(x => x.DeleteFlag, true));
            }
        }

        /// <summary>
        /// 判断明细内容中是否包含任一表单 ID 引用（忽略大小写）。
        /// </summary>
        /// <returns>包含任一引用时为 true。</returns>
        private static bool ContainsAnyFormReference(string? details, IReadOnlyCollection<string> formIds)
        {
            if (string.IsNullOrEmpty(details) || formIds.Count == 0)
            {
                return false;
            }

            return formIds.Any(id => details.Contains(id, StringComparison.OrdinalIgnoreCase));
        }

        public override async Task<int> DeleteAsync(string id)
        {
            var flowFormIds = GetWorkflowFormIds(x => x.Id == id);
            var result = await base.DeleteAsync(id);
            await ScheduleFlowDefinitionsCleanupAsync(flowFormIds);
            return result;
        }

        public override async Task<int> DeleteAsync(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var flowFormIds = GetWorkflowFormIds(x => idList.Contains(x.Id));
            var result = await base.DeleteAsync(idList);
            await ScheduleFlowDefinitionsCleanupAsync(flowFormIds);
            return result;
        }

        public override async Task<int> DeleteAsync(DynamicFilter filter)
        {
            var flowFormIds = GetWorkflowFormIds(filter);
            var result = await base.DeleteAsync(filter);
            await ScheduleFlowDefinitionsCleanupAsync(flowFormIds);
            return result;
        }

        private List<string> GetWorkflowFormIds(Expression<Func<FormDef, bool>> filter)
        {
            return Repository.Find(new QueryFindOptions<FormDef> { Filter = filter, Take = int.MaxValue })
                .Where(x => x.UsingWorkflow)
                .ToList()
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<string> GetWorkflowFormIds(DynamicFilter filter)
        {
            return Repository.Find(filter)
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

            if (TransactionScope.IsInTransaction)
            {
                TransactionScope.RegisterAfterCommit(DbContext, () => DeleteFlowDefinitionsAfterCommitAsync(formIds));
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
