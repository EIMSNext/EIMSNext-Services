using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class WorkbenchQueryApiService(IResolver resolver) : ApiServiceBase(resolver)
    {
        public List<WorkbenchCatalogAppViewModel> GetCatalog()
        {
            var corpId = IdentityContext.CurrentCorpId;
            var appIds = WorkbenchTargetResolver.GetAccessibleAppIds(Resolver, IdentityContext);
            var formIds = WorkbenchTargetResolver.GetAccessibleFormIds(Resolver, IdentityContext, null);
            var dashboardIds = WorkbenchTargetResolver.GetAccessibleDashboardIds(Resolver, IdentityContext, null);
            var apps = Resolver.GetRepository<AppDef>().Queryable
                .Where(x => x.CorpId == corpId && !x.DeleteFlag && appIds.Contains(x.Id))
                .OrderBy(x => x.SortIndex)
                .ThenBy(x => x.Name)
                .ToList();
            var forms = Resolver.GetRepository<FormDef>().Queryable
                .Where(x => x.CorpId == corpId && !x.DeleteFlag && formIds.Contains(x.Id))
                .ToDictionary(x => x.Id);
            var dashboards = Resolver.GetRepository<DashboardDef>().Queryable
                .Where(x => x.CorpId == corpId && !x.DeleteFlag && dashboardIds.Contains(x.Id))
                .OrderBy(x => x.Name)
                .ToList();
            var dashboardMap = dashboards.ToDictionary(x => x.Id);
            var chartItems = Resolver.GetRepository<DashboardItemDef>().Queryable
                .Where(x => x.CorpId == corpId && !x.DeleteFlag && x.ItemType == "chart" && dashboardIds.Contains(x.DashboardId))
                .OrderBy(x => x.Name)
                .ToList();
            var chartGroups = chartItems.GroupBy(x => x.DashboardId).ToDictionary(x => x.Key, x => x.ToList());

            return apps.Select(app => new WorkbenchCatalogAppViewModel
            {
                Id = app.Id,
                Name = app.Name,
                Icon = app.Icon,
                IconColor = app.IconColor,
                Menus = BuildMenuViewModels(app.AppMenus, forms, dashboardMap),
                Dashboards = dashboards
                    .Where(x => x.AppId == app.Id)
                    .Select(x => new WorkbenchCatalogDashboardViewModel
                    {
                        Id = x.Id,
                        Name = x.Name,
                        AppId = x.AppId,
                        Charts = chartGroups.TryGetValue(x.Id, out var items)
                            ? items.Select(item => new WorkbenchCatalogChartViewModel
                            {
                                Id = item.Id,
                                Name = item.Name,
                                DashboardId = item.DashboardId,
                                AppId = item.AppId
                            }).ToList()
                            : []
                    })
                    .Where(x => x.Charts.Count > 0)
                    .ToList()
            }).ToList();
        }

        public DashboardItemDef? GetChartItem(string dashboardItemId)
        {
            var corpId = IdentityContext.CurrentCorpId;
            var dashboardIds = WorkbenchTargetResolver.GetAccessibleDashboardIds(Resolver, IdentityContext, null);
            var item = Resolver.GetRepository<DashboardItemDef>().Get(dashboardItemId);
            if (item == null || item.CorpId != corpId || item.DeleteFlag || item.ItemType != "chart" || !dashboardIds.Contains(item.DashboardId))
            {
                return null;
            }

            return item;
        }

        private static List<WorkbenchCatalogMenuViewModel> BuildMenuViewModels(
            IEnumerable<AppMenu>? menus,
            Dictionary<string, FormDef> forms,
            Dictionary<string, DashboardDef> dashboards)
        {
            if (menus == null)
            {
                return [];
            }

            var result = new List<WorkbenchCatalogMenuViewModel>();
            foreach (var menu in menus.OrderBy(x => x.SortIndex))
            {
                if (menu.MenuType == FormType.Group)
                {
                    var children = BuildMenuViewModels(menu.SubMenus, forms, dashboards);
                    if (children.Count > 0)
                    {
                        result.Add(new WorkbenchCatalogMenuViewModel
                        {
                            Id = menu.MenuId,
                            Title = menu.Title,
                            TargetType = "group",
                            Icon = menu.Icon,
                            IconColor = menu.IconColor,
                            Children = children
                        });
                    }
                    continue;
                }

                if (menu.MenuType == FormType.Form && !forms.ContainsKey(menu.MenuId))
                {
                    continue;
                }

                if (menu.MenuType == FormType.Dashboard && !dashboards.ContainsKey(menu.MenuId))
                {
                    continue;
                }

                result.Add(new WorkbenchCatalogMenuViewModel
                {
                    Id = menu.MenuId,
                    Title = menu.Title,
                    TargetType = menu.MenuType == FormType.Dashboard ? WorkbenchTargetType.Dashboard : WorkbenchTargetType.Form,
                    Icon = menu.Icon,
                    IconColor = menu.IconColor,
                    Children = []
                });
            }

            return result;
        }
    }

    public class WorkbenchConfigApiService(IResolver resolver) : ApiServiceBase<WorkbenchConfig, WorkbenchConfigViewModel, IWorkbenchConfigService>(resolver)
    {
        protected override IQueryable<WorkbenchConfigViewModel> FilterByPermission()
        {
            var employeeId = CurrentEmployeeId;
            return CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == employeeId && !x.DeleteFlag)
                .Select(TVConvertor);
        }

        protected override Task AddAsyncCore(WorkbenchConfig entity)
        {
            entity.EmployeeId = CurrentEmployeeId;
            var duplicated = CoreService.All().Any(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                x.EmployeeId == entity.EmployeeId &&
                !x.DeleteFlag);
            if (duplicated)
            {
                throw new BadRequestException("工作台配置已存在");
            }

            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(WorkbenchConfig entity)
        {
            EnsureOwner(entity.EmployeeId);
            entity.EmployeeId = CurrentEmployeeId;
            return base.ReplaceAsyncCore(entity);
        }

        protected override Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            EnsureDeleteIds(ids);
            return base.DeleteAsyncCore(ids);
        }

        private void EnsureDeleteIds(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var count = CoreService.All().Count(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                x.EmployeeId == CurrentEmployeeId &&
                !x.DeleteFlag &&
                idList.Contains(x.Id));
            if (count != idList.Count)
            {
                throw new BadRequestException("工作台配置不存在");
            }
        }

        private void EnsureOwner(string employeeId)
        {
            if (employeeId != CurrentEmployeeId)
            {
                throw new BadRequestException("工作台配置不存在");
            }
        }

        private string CurrentEmployeeId => IdentityContext.CurrentEmployee?.Id ?? IdentityContext.CurrentUserID;
    }

    public class WorkbenchFavoriteApiService(IResolver resolver) : ApiServiceBase<WorkbenchFavorite, WorkbenchFavoriteViewModel, IWorkbenchFavoriteService>(resolver)
    {
        protected override IQueryable<WorkbenchFavoriteViewModel> FilterByPermission()
        {
            var corpId = IdentityContext.CurrentCorpId;
            var employeeId = CurrentEmployeeId;
            var appIds = WorkbenchTargetResolver.GetAccessibleAppIds(Resolver, IdentityContext).ToList();
            var formIds = WorkbenchTargetResolver.GetAccessibleFormIds(Resolver, IdentityContext, null);
            var dashboardIds = WorkbenchTargetResolver.GetAccessibleDashboardIds(Resolver, IdentityContext, null);

            return CoreService.All()
                .Where(x =>
                    x.CorpId == corpId &&
                    x.EmployeeId == employeeId &&
                    !x.DeleteFlag &&
                    ((x.TargetType == WorkbenchTargetType.App && appIds.Contains(x.TargetId)) ||
                     (x.TargetType == WorkbenchTargetType.Form && formIds.Contains(x.TargetId)) ||
                     (x.TargetType == WorkbenchTargetType.Dashboard && dashboardIds.Contains(x.TargetId))))
                .Select(TVConvertor);
        }

        protected override Task AddAsyncCore(WorkbenchFavorite entity)
        {
            entity.EmployeeId = CurrentEmployeeId;
            var target = ResolveTarget(entity.TargetType, entity.TargetId);
            if (target == null)
            {
                throw new BadRequestException("收藏目标不存在或无权限");
            }

            var duplicated = CoreService.All().Any(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                x.EmployeeId == entity.EmployeeId &&
                x.TargetType == target.TargetType &&
                x.TargetId == target.TargetId &&
                !x.DeleteFlag);
            if (duplicated)
            {
                return Task.CompletedTask;
            }

            ApplyTarget(entity, target);
            if (entity.SortIndex <= 0)
            {
                entity.SortIndex = CoreService.All().Count(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    x.EmployeeId == entity.EmployeeId &&
                    !x.DeleteFlag) + 1;
            }

            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(WorkbenchFavorite entity)
        {
            EnsureOwner(entity.EmployeeId);
            var target = ResolveTarget(entity.TargetType, entity.TargetId);
            if (target == null)
            {
                throw new BadRequestException("收藏目标不存在或无权限");
            }

            ApplyTarget(entity, target);
            entity.EmployeeId = CurrentEmployeeId;
            return base.ReplaceAsyncCore(entity);
        }

        protected override Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            EnsureDeleteIds(ids);
            return base.DeleteAsyncCore(ids);
        }

        private WorkbenchTargetInfo? ResolveTarget(string targetType, string targetId)
        {
            return WorkbenchTargetResolver.Resolve(Resolver, IdentityContext, targetType, targetId);
        }

        private static void ApplyTarget(WorkbenchFavorite entity, WorkbenchTargetInfo target)
        {
            entity.TargetType = target.TargetType;
            entity.TargetId = target.TargetId;
            entity.AppId = target.AppId;
            entity.Title = target.Title;
            entity.Icon = target.Icon;
            entity.IconColor = target.IconColor;
        }

        private void EnsureDeleteIds(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var count = CoreService.All().Count(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                x.EmployeeId == CurrentEmployeeId &&
                !x.DeleteFlag &&
                idList.Contains(x.Id));
            if (count != idList.Count)
            {
                throw new BadRequestException("收藏不存在");
            }
        }

        private void EnsureOwner(string employeeId)
        {
            if (employeeId != CurrentEmployeeId)
            {
                throw new BadRequestException("收藏不存在");
            }
        }

        private string CurrentEmployeeId => IdentityContext.CurrentEmployee?.Id ?? IdentityContext.CurrentUserID;
    }

    public class WorkbenchRecentVisitApiService(IResolver resolver) : ApiServiceBase<WorkbenchRecentVisit, WorkbenchRecentVisitViewModel, IWorkbenchRecentVisitService>(resolver)
    {
        private const int MaxRecentVisitCount = 10;

        protected override IQueryable<WorkbenchRecentVisitViewModel> FilterByPermission()
        {
            var corpId = IdentityContext.CurrentCorpId;
            var employeeId = CurrentEmployeeId;
            var formIds = WorkbenchTargetResolver.GetAccessibleFormIds(Resolver, IdentityContext, null);
            var dashboardIds = WorkbenchTargetResolver.GetAccessibleDashboardIds(Resolver, IdentityContext, null);

            return CoreService.All()
                .Where(x =>
                    x.CorpId == corpId &&
                    x.EmployeeId == employeeId &&
                    !x.DeleteFlag &&
                    ((x.TargetType == WorkbenchTargetType.Form && formIds.Contains(x.TargetId)) ||
                     (x.TargetType == WorkbenchTargetType.Dashboard && dashboardIds.Contains(x.TargetId))))
                .Select(TVConvertor);
        }

        protected override async Task AddAsyncCore(WorkbenchRecentVisit entity)
        {
            entity.EmployeeId = CurrentEmployeeId;
            var target = ResolveTarget(entity.TargetType, entity.TargetId);
            if (target == null || !IsRecentTargetType(target.TargetType))
            {
                throw new BadRequestException("最近使用目标不存在或无权限");
            }

            var duplicated = CoreService.All().Any(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                x.EmployeeId == entity.EmployeeId &&
                x.TargetType == target.TargetType &&
                x.TargetId == target.TargetId &&
                !x.DeleteFlag);
            if (duplicated)
            {
                throw new BadRequestException("最近使用记录已存在");
            }

            ApplyTarget(entity, target);
            entity.VisitCount = 1;
            entity.LastVisitTime = Now();
            await base.AddAsyncCore(entity);
            await PruneRecentVisitsAsync(entity.EmployeeId);
        }

        protected override async Task<ReplaceOneResult> ReplaceAsyncCore(WorkbenchRecentVisit entity)
        {
            EnsureOwner(entity.EmployeeId);
            var target = ResolveTarget(entity.TargetType, entity.TargetId);
            if (target == null || !IsRecentTargetType(target.TargetType))
            {
                throw new BadRequestException("最近使用目标不存在或无权限");
            }

            ApplyTarget(entity, target);
            entity.EmployeeId = CurrentEmployeeId;
            entity.VisitCount = Math.Max(0, entity.VisitCount) + 1;
            entity.LastVisitTime = Now();
            var result = await base.ReplaceAsyncCore(entity);
            await PruneRecentVisitsAsync(entity.EmployeeId);
            return result;
        }

        protected override Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            EnsureDeleteIds(ids);
            return base.DeleteAsyncCore(ids);
        }

        private WorkbenchTargetInfo? ResolveTarget(string targetType, string targetId)
        {
            return WorkbenchTargetResolver.Resolve(Resolver, IdentityContext, targetType, targetId);
        }

        private static void ApplyTarget(WorkbenchRecentVisit entity, WorkbenchTargetInfo target)
        {
            entity.TargetType = target.TargetType;
            entity.TargetId = target.TargetId;
            entity.AppId = target.AppId;
            entity.Title = target.Title;
            entity.Icon = target.Icon;
            entity.IconColor = target.IconColor;
        }

        private static bool IsRecentTargetType(string targetType)
        {
            return targetType == WorkbenchTargetType.Form ||
                   targetType == WorkbenchTargetType.Dashboard;
        }

        private void EnsureDeleteIds(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var count = CoreService.All().Count(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                x.EmployeeId == CurrentEmployeeId &&
                !x.DeleteFlag &&
                idList.Contains(x.Id));
            if (count != idList.Count)
            {
                throw new BadRequestException("最近使用记录不存在");
            }
        }

        private void EnsureOwner(string employeeId)
        {
            if (employeeId != CurrentEmployeeId)
            {
                throw new BadRequestException("最近使用记录不存在");
            }
        }

        private async Task PruneRecentVisitsAsync(string employeeId)
        {
            var records = CoreService.All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    x.EmployeeId == employeeId &&
                    !x.DeleteFlag)
                .OrderByDescending(x => x.LastVisitTime)
                .ThenByDescending(x => x.CreateTime)
                .ToList();

            var seenTargets = new HashSet<string>();
            var keptCount = 0;
            var idsToDelete = new List<string>();
            foreach (var record in records)
            {
                var targetKey = $"{record.TargetType}:{record.TargetId}";
                if (!seenTargets.Add(targetKey) || keptCount >= MaxRecentVisitCount)
                {
                    idsToDelete.Add(record.Id);
                    continue;
                }

                keptCount++;
            }

            if (idsToDelete.Count > 0)
            {
                await CoreService.DeleteAsync(idsToDelete);
            }
        }

        private string CurrentEmployeeId => IdentityContext.CurrentEmployee?.Id ?? IdentityContext.CurrentUserID;

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public record WorkbenchTargetInfo(string TargetType, string TargetId, string AppId, string Title, string Icon, string IconColor);

    public static class WorkbenchTargetResolver
    {
        public static WorkbenchTargetInfo? Resolve(IResolver resolver, IIdentityContext identityContext, string targetType, string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return null;
            }

            var corpId = identityContext.CurrentCorpId;
            var appIds = GetAccessibleAppIds(resolver, identityContext);
            var appRepo = resolver.GetRepository<AppDef>();

            if (targetType == WorkbenchTargetType.App)
            {
                var app = appRepo.Get(targetId);
                if (app == null || app.CorpId != corpId || app.DeleteFlag || !appIds.Contains(app.Id))
                {
                    return null;
                }

                return new WorkbenchTargetInfo(WorkbenchTargetType.App, app.Id, app.Id, app.Name, app.Icon, app.IconColor);
            }

            if (targetType == WorkbenchTargetType.Form)
            {
                var form = resolver.GetRepository<FormDef>().Get(targetId);
                var formIds = GetAccessibleFormIds(resolver, identityContext, form?.AppId);
                if (form == null || form.CorpId != corpId || form.DeleteFlag || !formIds.Contains(form.Id))
                {
                    return null;
                }

                var app = appRepo.Get(form.AppId);
                var menu = app?.AppMenus == null ? null : AppMenuHelper.FindMenu(app.AppMenus, form.Id);
                return new WorkbenchTargetInfo(
                    WorkbenchTargetType.Form,
                    form.Id,
                    form.AppId,
                    string.IsNullOrWhiteSpace(menu?.Title) ? form.Name : menu!.Title,
                    menu?.Icon ?? string.Empty,
                    menu?.IconColor ?? string.Empty);
            }

            if (targetType == WorkbenchTargetType.Dashboard)
            {
                var dashboard = resolver.GetRepository<DashboardDef>().Get(targetId);
                var dashboardIds = GetAccessibleDashboardIds(resolver, identityContext, dashboard?.AppId);
                if (dashboard == null || dashboard.CorpId != corpId || dashboard.DeleteFlag || !dashboardIds.Contains(dashboard.Id))
                {
                    return null;
                }

                var app = appRepo.Get(dashboard.AppId);
                var menu = app?.AppMenus == null ? null : AppMenuHelper.FindMenu(app.AppMenus, dashboard.Id);
                return new WorkbenchTargetInfo(
                    WorkbenchTargetType.Dashboard,
                    dashboard.Id,
                    dashboard.AppId,
                    string.IsNullOrWhiteSpace(menu?.Title) ? dashboard.Name : menu!.Title,
                    menu?.Icon ?? string.Empty,
                    menu?.IconColor ?? string.Empty);
            }

            return null;
        }

        public static List<string> GetAccessibleFormIds(IResolver resolver, IIdentityContext identityContext, string? appId)
        {
            var corpId = identityContext.CurrentCorpId;
            var evaluator = resolver.Resolve<TenantAccessEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return resolver.GetRepository<FormDef>().Queryable
                    .Where(x =>
                        x.CorpId == corpId &&
                        !x.DeleteFlag &&
                        (string.IsNullOrEmpty(appId) || x.AppId == appId))
                    .Select(x => x.Id)
                    .ToList();
            }

            if (identityContext.IdentityType == IdentityType.AppAdmin)
            {
                var formIds = evaluator.GetUsageFormIdsForCurrentEmployee(appId);
                var manageableAppIds = evaluator.GetSnapshot().ManageableAppIds;
                return resolver.GetRepository<FormDef>().Queryable
                    .Where(x =>
                        x.CorpId == corpId &&
                        !x.DeleteFlag &&
                        (string.IsNullOrEmpty(appId) || x.AppId == appId) &&
                        (formIds.Contains(x.Id) || manageableAppIds.Contains(x.AppId)))
                    .Select(x => x.Id)
                    .ToList();
            }

            if (IdentityType.Employee_Admins.HasFlag(identityContext.IdentityType))
            {
                return evaluator.GetUsageFormIdsForCurrentEmployee(appId);
            }

            return [];
        }

        public static List<string> GetAccessibleDashboardIds(IResolver resolver, IIdentityContext identityContext, string? appId)
        {
            return resolver.Resolve<TenantAccessEvaluator>().GetUsageDashboardIdsForCurrentEmployee(appId);
        }

        public static HashSet<string> GetAccessibleAppIds(IResolver resolver, IIdentityContext identityContext)
        {
            var corpId = identityContext.CurrentCorpId;
            var evaluator = resolver.Resolve<TenantAccessEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return resolver.GetRepository<AppDef>().Queryable
                    .Where(x => x.CorpId == corpId && !x.DeleteFlag)
                    .Select(x => x.Id)
                    .ToHashSet();
            }

            if (identityContext.IdentityType == IdentityType.AppAdmin)
            {
                return evaluator.GetUsageAppIdsForCurrentEmployee()
                    .Concat(evaluator.GetSnapshot().ManageableAppIds)
                    .Distinct()
                    .ToHashSet();
            }

            if (IdentityType.Employee_Admins.HasFlag(identityContext.IdentityType))
            {
                return evaluator.GetUsageAppIdsForCurrentEmployee().ToHashSet();
            }

            return [];
        }
    }
}
