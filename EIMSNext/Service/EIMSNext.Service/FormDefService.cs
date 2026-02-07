using EIMSNext.ApiClient.Flow;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
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
            var appRepo = Resolver.GetRepository<App>();
            var app = appRepo.Get(entities.First().AppId, session)!;
            var maxIndex = app.AppMenus.Count == 0 ? 0 : app.AppMenus.Max(x => x.SortIndex);
            entities.ForEach(e =>
            {
                maxIndex = maxIndex + 100;
                app.AppMenus.Add(new AppMenu { MenuId = e.Id, Icon = "", IconColor = "", MenuType = e.Type, Title = e.Name, SortIndex = maxIndex });
            });
            appRepo.Replace(app, session);

            return;
        }

        protected override Task BeforeReplace(FormDef entity, IClientSessionHandle? session)
        {

            return base.BeforeReplace(entity, session);
        }

        protected override async Task AfterReplace(FormDef entity, IClientSessionHandle? session)
        {
            await base.AfterReplace(entity, session);
            var appRepo = Resolver.GetRepository<App>();
            var app = appRepo.Get(entity.AppId, session)!;

            var menu = app.AppMenus.FirstOrDefault(x => x.MenuId == entity.Id);
            if (menu != null)
            {
                menu.Title = entity.Name;
                appRepo.Replace(app, session);
            }
        }
        protected override async Task AfterUpdate(FilterDefinition<FormDef> filter, UpdateDefinition<FormDef> update, bool upsert, IClientSessionHandle? session)
        {
            await base.AfterUpdate(filter, update, upsert, session);
            var updated = Context.SessionStore.GetAll<FormDef>(Cache.DataVersion.New);
            if (!updated.Any())
            {
                updated = await Collection.Find(filter).ToListAsync();
            }
            if (updated.Any())
            {
                var appRepo = Resolver.GetRepository<App>();
                var app = appRepo.Get(updated.First().AppId, session)!;

                updated.ForEach(e =>
                {
                    var menu = app.AppMenus.FirstOrDefault(x => x.MenuId == e.Id);
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

            var appRepo = Resolver.GetRepository<App>();

            // 按 AppId 分组，批量处理
            var appIds = deletedForms.Select(f => f.AppId).Distinct();
            foreach (var appId in appIds)
            {
                var app = appRepo.Get(appId, session);
                if (app == null) continue;

                // 移除所有被删除 FormDef 对应的菜单
                var removedCount = app.AppMenus.RemoveAll(m => deletedForms.Any(f => f.Id == m.MenuId));
                if (removedCount > 0)
                {
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
                //删除所有待办
                var todoRepo = Resolver.GetRepository<Wf_Todo>();
                await todoRepo.DeleteAsync(todoRepo.FilterBuilder.In(x => x.FormId, flowFormIds), session);

                //废弃所有流程实例
                await _flowClient.DeleteDef(new DeleteRequest { DeleteDef = true, FormIds = flowFormIds }, Context.AccessToken);
            }
        }

    }
}
