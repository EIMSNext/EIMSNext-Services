using System.Linq.Expressions;
using HKH.Mef2.Integration;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EIMSNext.Service
{
	public class DashboardDefService(IResolver resolver) : EntityServiceBase<DashboardDef>(resolver), IDashboardDefService
	{
        private static readonly HashSet<int> ValidRefreshIntervals = [1, 3, 5, 10, 15, 30, 60, 180];

        protected override Task BeforeAdd(IEnumerable<DashboardDef> entities)
        {
            foreach (var entity in entities)
            {
                PrepareEntity(entity);
            }

            return base.BeforeAdd(entities);
        }

        protected override Task BeforeReplace(DashboardDef entity)
        {
            PrepareEntity(entity);
            return base.BeforeReplace(entity);
        }

        protected override async Task AfterAdd(IEnumerable<DashboardDef> entities)
        {
            await base.AfterAdd(entities);
            var appRepo = Resolver.GetRepository<AppDef>();
            var app = (await appRepo.GetAsync(entities.First().AppId).ConfigureAwait(false))!;
            var maxIndex = app.AppMenus.Count == 0 ? 0 : app.AppMenus.Max(x => x.SortIndex);
            entities.ForEach(e =>
            {
                maxIndex = maxIndex + 100;
                app.AppMenus.Add(new AppMenu { MenuId = e.Id, Icon = "", IconColor = "", MenuType = FormType.Dashboard, Title = e.Name, SortIndex = maxIndex });
            });
            await appRepo.ReplaceAsync(app).ConfigureAwait(false);
        }

        protected override async Task AfterReplace(DashboardDef entity)
        {
            await base.AfterReplace(entity);
            var appRepo = Resolver.GetRepository<AppDef>();
            var app = (await appRepo.GetAsync(entity.AppId).ConfigureAwait(false))!;

            var menu = AppMenuHelper.FindMenu(app.AppMenus, entity.Id);
            if (menu != null)
            {
                menu.Title = entity.Name;
                await appRepo.ReplaceAsync(app).ConfigureAwait(false);
            }
        }

        protected override async Task AfterUpdate(
            Expression<Func<DashboardDef, bool>> filter,
            Action<UpdateSettersBuilder<DashboardDef>> setters)
        {
            await base.AfterUpdate(filter, setters);
            var updated = Context.ScopeCache.GetAll<DashboardDef>(Cache.DataVersion.New);
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
                    PrepareEntity(e);
                    var menu = AppMenuHelper.FindMenu(app.AppMenus, e.Id);
                    if (menu != null) menu.Title = e.Name;
                });
                await appRepo.ReplaceAsync(app).ConfigureAwait(false);
            }
        }

        protected override async Task AfterDelete(Expression<Func<DashboardDef, bool>> filter)
        {
            await base.AfterDelete(filter);
            // 同 FormDefService.AfterDelete：软删除已经生效，必须忽略全局 `!DeleteFlag` 过滤，
            // 否则读不到刚删掉的仪表盘，子项逻辑删除与 App 菜单清理都会失效。
            var deletedDashboards = Repository.Queryable
                .IgnoreQueryFilters()
                .Where(filter)
                .ToList();
            if (deletedDashboards.Count == 0)
            {
                return;
            }

            var dashboardIds = deletedDashboards.Select(x => x.Id).ToList();
            var dashboardItemRepo = Resolver.GetRepository<DashboardItemDef>();
            await dashboardItemRepo.UpdateManyAsync(
                x => !x.DeleteFlag && dashboardIds.Contains(x.DashboardId),
                setters => setters.SetProperty(x => x.DeleteFlag, true));

            var appRepo = Resolver.GetRepository<AppDef>();
            var appIds = deletedDashboards.Select(x => x.AppId).Distinct();
            foreach (var appId in appIds)
            {
                var app = appRepo.Get(appId);
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
                    appRepo.Replace(app);
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
