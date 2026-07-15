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
        private static readonly HashSet<int> ValidRefreshIntervals = [1, 3, 5, 10, 15, 30, 60, 180];

        protected override Task BeforeAdd(IEnumerable<DashboardDef> entities, IClientSessionHandle? session)
        {
            foreach (var entity in entities)
            {
                PrepareEntity(entity);
            }

            return base.BeforeAdd(entities, session);
        }

        protected override Task BeforeReplace(DashboardDef entity, IClientSessionHandle? session)
        {
            PrepareEntity(entity);
            return base.BeforeReplace(entity, session);
        }

        protected override async Task AfterAdd(IEnumerable<DashboardDef> entities, IClientSessionHandle? session)
        {
            await base.AfterAdd(entities, session);
            var appRepo = Resolver.GetRepository<AppDef>();
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
            var appRepo = Resolver.GetRepository<AppDef>();
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
            var updated = Context.ScopeCache.GetAll<DashboardDef>(Cache.DataVersion.New);
            if (!updated.Any())
            {
                updated = await Collection.Find(filter).ToListAsync();
            }

            if (updated.Any())
            {
                var appRepo = Resolver.GetRepository<AppDef>();
                var dashboardRepo = Resolver.GetRepository<DashboardDef>();
                var app = appRepo.Get(updated.First().AppId, session)!;

                updated.ForEach(e =>
                {
                    PrepareEntity(e);
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

            var dashboardIds = deletedDashboards.Select(x => x.Id).ToList();
            var dashboardItemRepo = Resolver.GetRepository<DashboardItemDef>();
            await dashboardItemRepo.UpdateManyAsync(
                dashboardItemRepo.FilterBuilder.And(
                    dashboardItemRepo.FilterBuilder.Eq(x => x.DeleteFlag, false),
                    dashboardItemRepo.FilterBuilder.In(x => x.DashboardId, dashboardIds)),
                dashboardItemRepo.UpdateBuilder.Set(x => x.DeleteFlag, true),
                session: session);

            var appRepo = Resolver.GetRepository<AppDef>();
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

        private static void PrepareEntity(DashboardDef entity)
        {
            if (!ValidRefreshIntervals.Contains(entity.AutoRefreshIntervalMinutes))
            {
                entity.AutoRefreshIntervalMinutes = 15;
            }

            entity.PublishMembers ??= [];
        }
    }
}
