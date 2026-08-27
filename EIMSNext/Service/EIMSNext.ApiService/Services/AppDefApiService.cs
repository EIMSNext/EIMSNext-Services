using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class AppDefApiService(IResolver resolver) : ApiServiceBase<AppDef, AppDefViewModel, IAppDefService>(resolver)
    {
        public async Task<AppDef> CreateGroup(CreateAppGroupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException("分组名称不能为空");
            }

            var app = await GetManageableAppAsync(request.AppId);
            var menu = new AppMenu
            {
                MenuId = Guid.NewGuid().ToString("N"),
                Title = request.Name.Trim(),
                MenuType = FormType.Group,
                SortIndex = (app.AppMenus.Count + 1) * 100,
                SubMenus = []
            };

            app.AppMenus.Add(menu);
            AppMenuHelper.Normalize(app.AppMenus);
            await CoreService.ReplaceAsync(app);
            return app;
        }

        public async Task<AppDef> EditGroup(EditAppGroupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.MenuId) || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException("分组名称不能为空");
            }

            var app = await GetManageableAppAsync(request.AppId);
            var menu = AppMenuHelper.FindMenu(app.AppMenus, request.MenuId);
            if (menu == null || menu.MenuType != FormType.Group)
            {
                throw new BadRequestException("分组不存在");
            }

            menu.Title = request.Name.Trim();
            await CoreService.ReplaceAsync(app);
            return app;
        }

        public async Task<AppDef> EditMenu(EditAppMenuRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.MenuId) || string.IsNullOrWhiteSpace(request.Name))
            {
                throw new BadRequestException("菜单名称不能为空");
            }

            var app = await GetManageableAppAsync(request.AppId);
            var menu = AppMenuHelper.FindMenu(app.AppMenus, request.MenuId);
            if (menu == null)
            {
                throw new BadRequestException("菜单不存在");
            }

            if (menu.MenuType == FormType.Group)
            {
                throw new BadRequestException("分组请使用专用接口修改");
            }

            var name = request.Name.Trim();
            menu.Title = name;
            menu.Icon = request.Icon ?? string.Empty;
            menu.IconColor = request.IconColor ?? string.Empty;

            if (menu.MenuType == FormType.Form)
            {
                var formService = Resolver.GetService<FormDef>();
                var form = formService.Get(request.MenuId);
                if (form == null || form.CorpId != IdentityContext.CurrentCorpId || form.DeleteFlag)
                {
                    throw new BadRequestException("表单不存在");
                }

                form.Name = name;
                await formService.ReplaceAsync(form);
            }
            else if (menu.MenuType == FormType.Dashboard)
            {
                var dashService = Resolver.GetService<DashboardDef>();
                var dash = dashService.Get(request.MenuId);
                if (dash == null || dash.CorpId != IdentityContext.CurrentCorpId || dash.DeleteFlag)
                {
                    throw new BadRequestException("仪表盘不存在");
                }

                dash.Name = name;
                await dashService.ReplaceAsync(dash);
            }

            await CoreService.ReplaceAsync(app);
            return app;
        }

        public async Task<AppDef> DeleteGroup(DeleteAppGroupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.MenuId))
            {
                throw new BadRequestException("请求参数无效");
            }

            var app = await GetManageableAppAsync(request.AppId);
            var menu = AppMenuHelper.FindMenu(app.AppMenus, request.MenuId);
            if (menu == null)
            {
                throw new BadRequestException("分组不存在");
            }

            if (menu.MenuType != FormType.Group)
            {
                throw new BadRequestException("只能删除分组");
            }

            if (menu.SubMenus?.Count > 0)
            {
                throw new BadRequestException("当前分组下存在子菜单，不能删除");
            }

            if (!AppMenuHelper.RemoveMenu(app.AppMenus, request.MenuId))
            {
                throw new BadRequestException("删除失败");
            }

            AppMenuHelper.Normalize(app.AppMenus);
            await CoreService.ReplaceAsync(app);
            return app;
        }

        public async Task<AppDef> SaveMenus(SaveAppMenusRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.AppId))
            {
                throw new BadRequestException("应用ID不能为空");
            }

            if (!AppMenuHelper.ValidateTree(request.AppMenus ?? []))
            {
                throw new BadRequestException("分组内不能再包含分组");
            }

            var app = await GetManageableAppAsync(request.AppId);
            app.AppMenus = RebuildSortableMenuTree(request.AppMenus ?? [], app.AppMenus ?? []);
            await CoreService.ReplaceAsync(app);
            return app;
        }

        protected override async Task AddAsyncCore(AppDef entity)
        {
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            evaluator.EnsureCanCreateApp();
            ValidateHomeEntries(entity);

            await base.AddAsyncCore(entity);
            await evaluator.SyncCreatedAppToNormalTenantAdminGroupsAsync(entity.Id);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(AppDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.Id);
            ValidateHomeEntries(entity);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            foreach (var id in idList)
            {
                evaluator.EnsureCanDeleteApp(id);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private async Task<AppDef> GetManageableAppAsync(string appId)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(appId);
            var app = await CoreService.GetAsync(appId);
            if (app == null || app.CorpId != IdentityContext.CurrentCorpId || app.DeleteFlag)
            {
                throw new NotFoundException("应用不存在");
            }

            return app;
        }

        private static void ValidateHomeEntries(AppDef entity)
        {
            entity.HomeEntryIds = (entity.HomeEntryIds ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();

            if (entity.HomeEntryIds.Count == 0)
            {
                return;
            }

            foreach (var entryId in entity.HomeEntryIds)
            {
                var menu = AppMenuHelper.FindMenu(entity.AppMenus ?? [], entryId);
                if (menu == null || menu.MenuType != FormType.Dashboard)
                {
                    throw new BadRequestException("应用首页入口必须指向当前应用菜单中的仪表盘");
                }
            }
        }

        private static List<AppMenu> RebuildSortableMenuTree(List<AppMenu> submittedMenus, List<AppMenu> existingMenus)
        {
            var existingFlat = AppMenuHelper.Flatten(existingMenus).ToList();
            var existingIds = existingFlat
                .Where(x => !string.IsNullOrWhiteSpace(x.MenuId))
                .Select(x => x.MenuId)
                .ToList();
            var submittedFlat = AppMenuHelper.Flatten(submittedMenus).ToList();
            var submittedIds = submittedFlat
                .Where(x => !string.IsNullOrWhiteSpace(x.MenuId))
                .Select(x => x.MenuId)
                .ToList();

            if (existingFlat.Any(x => string.IsNullOrWhiteSpace(x.MenuId)) ||
                existingIds.Count != existingFlat.Count ||
                existingIds.Count != existingIds.Distinct().Count())
            {
                throw new BadRequestException("当前应用菜单包含无效数据");
            }

            if (submittedFlat.Any(x => string.IsNullOrWhiteSpace(x.MenuId)) || submittedIds.Count != submittedFlat.Count)
            {
                throw new BadRequestException("菜单包含无效数据");
            }

            if (submittedIds.Count != submittedIds.Distinct().Count())
            {
                throw new BadRequestException("菜单包含重复数据");
            }

            var existingIdSet = existingIds.ToHashSet();
            var submittedIdSet = submittedIds.ToHashSet();
            if (submittedIdSet.Any(x => !existingIdSet.Contains(x)) || existingIdSet.Any(x => !submittedIdSet.Contains(x)))
            {
                throw new BadRequestException("菜单数据与当前应用不一致");
            }

            var existingById = existingFlat.ToDictionary(x => x.MenuId, x => x);
            foreach (var submitted in submittedFlat)
            {
                if (existingById[submitted.MenuId].MenuType != submitted.MenuType)
                {
                    throw new BadRequestException("菜单类型不能修改");
                }
            }

            return AppMenuHelper.Normalize(CloneSortableMenuTree(submittedMenus, existingById));
        }

        private static List<AppMenu> CloneSortableMenuTree(IEnumerable<AppMenu> submittedMenus, Dictionary<string, AppMenu> existingById)
        {
            return submittedMenus.Select(submitted =>
            {
                var existing = existingById[submitted.MenuId];
                return new AppMenu
                {
                    MenuId = existing.MenuId,
                    Title = existing.Title,
                    Icon = existing.Icon,
                    IconColor = existing.IconColor,
                    MenuType = existing.MenuType,
                    SortIndex = existing.SortIndex,
                    Editable = existing.Editable,
                    Deletable = existing.Deletable,
                    ListComponent = existing.ListComponent,
                    SubMenus = existing.MenuType == FormType.Group
                        ? CloneSortableMenuTree(submitted.SubMenus ?? [], existingById)
                        : null,
                };
            }).ToList();
        }
    }
}
