using EIMSNext.ApiClient.Flow;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using MongoDB.Driver;

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
                NormalizeFieldMetadata(entity);
                ValidateFieldIds(entity);
            }
            return base.BeforeAdd(entities, session);
        }

        protected override Task BeforeReplace(FormDef entity, IClientSessionHandle? session)
        {
            NormalizeFieldMetadata(entity);
            ValidateFieldIds(entity);
            return base.BeforeReplace(entity, session);
        }

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
                var todoRepo = Resolver.GetRepository<Wf_Todo>();
                await todoRepo.DeleteAsync(todoRepo.FilterBuilder.In(x => x.FormId, flowFormIdList), session);

                //废弃所有流程实例
                await _flowClient.DeleteDef(new DeleteRequest { DeleteDef = true, FormIds = flowFormIdList }, Context.AccessToken);
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

            var authGroupRepo = Resolver.GetRepository<AuthGroup>();
            await authGroupRepo.UpdateManyAsync(
                authGroupRepo.FilterBuilder.And(
                    authGroupRepo.FilterBuilder.Eq(x => x.DeleteFlag, false),
                    authGroupRepo.FilterBuilder.In(x => x.FormId, formIds)),
                authGroupRepo.UpdateBuilder.Set(x => x.DeleteFlag, true),
                session: session);

            var itemRepo = Resolver.GetRepository<DashboardItemDef>();
            var deletedFormIdSet = formIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var embeddedItems = itemRepo.Queryable
                .Where(x => corpIds.Contains(x.CorpId) && !x.DeleteFlag)
                .ToList()
                .Where(x => deletedFormIdSet.Any(id => x.Details.Contains(id, StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.Id)
                .ToList();
            if (embeddedItems.Count > 0)
            {
                await itemRepo.UpdateManyAsync(
                    itemRepo.FilterBuilder.In(x => x.Id, embeddedItems),
                    itemRepo.UpdateBuilder.Set(x => x.DeleteFlag, true),
                    session: session);
            }

        }

    }
}
