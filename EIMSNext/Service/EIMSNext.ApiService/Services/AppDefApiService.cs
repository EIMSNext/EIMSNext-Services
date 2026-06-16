using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
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

            if (!AppMenuHelper.ValidateTree(request.AppMenus))
            {
                throw new BadRequestException("分组内不能再包含分组");
            }

            var app = await GetManageableAppAsync(request.AppId);
            app.AppMenus = AppMenuHelper.Normalize(request.AppMenus ?? []);
            await CoreService.ReplaceAsync(app);
            return app;
        }

        protected override async Task AddAsyncCore(AppDef entity)
        {
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            evaluator.EnsureCanCreateApp();
            ValidateHomeEntry(entity);

            await base.AddAsyncCore(entity);
            await evaluator.SyncCreatedAppToNormalAdminGroupsAsync(entity.Id);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(AppDef entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.Id);
            ValidateHomeEntry(entity);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var id in idList)
            {
                evaluator.EnsureCanDeleteApp(id);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private async Task<AppDef> GetManageableAppAsync(string appId)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(appId);
            var app = await CoreService.GetAsync(appId);
            if (app == null || app.CorpId != IdentityContext.CurrentCorpId || app.DeleteFlag)
            {
                throw new NotFoundException("应用不存在");
            }

            return app;
        }

        private static void ValidateHomeEntry(AppDef entity)
        {
            if (string.IsNullOrWhiteSpace(entity.HomeEntryId))
            {
                entity.HomeEntryId = null;
                return;
            }

            var menu = AppMenuHelper.FindMenu(entity.AppMenus ?? [], entity.HomeEntryId);
            if (menu == null || menu.MenuType == FormType.Group)
            {
                throw new BadRequestException("应用首页入口必须指向当前应用菜单中的表单或仪表盘");
            }
        }
    }
}
