using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Core;
using MongoDB.Driver;
using EIMSNext.Core.Query;

namespace EIMSNext.Service
{
	public class DashboardDefService(IResolver resolver) : EntityServiceBase<DashboardDef>(resolver), IDashboardDefService
	{
        protected override async Task AfterAdd(IEnumerable<DashboardDef> entities, IClientSessionHandle? session)
        {
            await base.AfterAdd(entities, session);
            var appRepo = Resolver.GetRepository<App>();
            var app = appRepo.Get(entities.First().AppId, session)!;
            var maxIndex = app.AppMenus.Count == 0 ? 0 : app.AppMenus.Max(x => x.SortIndex);
            entities.ForEach(e =>
            {
                maxIndex = maxIndex + 100;
                app.AppMenus.Add(new AppMenu { MenuId = e.Id, Icon = "", IconColor = "", MenuType = FormType.Dashboard, Title = e.Name, SortIndex = maxIndex });
            });
            appRepo.Replace(app, session);

            return;
        }

        protected override async Task AfterReplace(DashboardDef entity, IClientSessionHandle? session)
        {
            await base.AfterReplace(entity, session);
            var appRepo = Resolver.GetRepository<App>();
            var app = appRepo.Get(entity.AppId, session)!;

            var menu = AppMenuHelper.FindMenu(app.AppMenus, entity.Id);
            if (menu != null)
            {
                menu.Title = entity.Name;
                appRepo.Replace(app, session);
            }
        }

        protected override async Task AfterUpdate(FilterDefinition<DashboardDef> filter, UpdateDefinition<DashboardDef> update, bool upsert, IClientSessionHandle? session)
        {
            await base.AfterUpdate(filter, update, upsert, session);
            var updated = Context.SessionStore.GetAll<DashboardDef>(Cache.DataVersion.New);
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
                    var menu = AppMenuHelper.FindMenu(app.AppMenus, e.Id);
                    if (menu != null) menu.Title = e.Name;
                });
                appRepo.Replace(app, session);
            }
        }

        protected override async Task AfterDelete(FilterDefinition<DashboardDef> filter, IClientSessionHandle? session)
        {
            await base.AfterDelete(filter, session);
            var deletedDashboards = Repository.Find(new MongoFindOptions<DashboardDef> { Filter = filter }, session).ToList();
            if (deletedDashboards.Count == 0)
            {
                return;
            }

            var appRepo = Resolver.GetRepository<App>();
            var appIds = deletedDashboards.Select(x => x.AppId).Distinct();
            foreach (var appId in appIds)
            {
                var app = appRepo.Get(appId, session);
                if (app == null) continue;

                var removedCount = 0;
                foreach (var dash in deletedDashboards.Where(x => x.AppId == appId))
                {
                    if (AppMenuHelper.RemoveMenu(app.AppMenus, dash.Id))
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
        }
    }
}
