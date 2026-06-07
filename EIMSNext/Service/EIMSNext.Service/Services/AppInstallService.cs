using System.Text.Json.Nodes;
using System.Text.Json;
using EIMSNext.Core.Entities;
using EIMSNext.Core;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class AppInstallService(IResolver resolver) : IAppInstallService
    {
        private readonly IResolver _resolver = resolver;

        public async Task<string> InstallAsync(string appProfileId)
        {
            var profileRepo = _resolver.GetRepository<AppProfile>();
            var appTemplateRepo = _resolver.GetRepository<AppTemplate>();
            var formTemplateRepo = _resolver.GetRepository<FormTemplate>();
            var dashboardTemplateRepo = _resolver.GetRepository<DashboardTemplate>();
            var dashboardItemTemplateRepo = _resolver.GetRepository<DashboardItemTemplate>();
            var wfTemplateRepo = _resolver.GetRepository<WfDefinitionTemplate>();
            var printTemplateTemplateRepo = _resolver.GetRepository<PrintDefTemplate>();
            var appDefRepo = _resolver.GetRepository<AppDef>();
            var formDefRepo = _resolver.GetRepository<FormDef>();
            var dashboardDefRepo = _resolver.GetRepository<DashboardDef>();
            var dashboardItemDefRepo = _resolver.GetRepository<DashboardItemDef>();
            var wfDefRepo = _resolver.GetRepository<Wf_Definition>();
            var printTemplateRepo = _resolver.GetRepository<PrintDef>();
            var authGroupTemplateRepo = _resolver.GetRepository<AuthGroupTemplate>();
            var authGroupRepo = _resolver.GetRepository<AuthGroup>();

            var profile = profileRepo.Get(appProfileId) ?? throw new InvalidOperationException("应用档案不存在");
            var appTemplate = appTemplateRepo.Get(profile.TemplateId) ?? throw new InvalidOperationException("应用模板不存在");

            List<FormTemplate> formTemplates = formTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            List<DashboardTemplate> dashboardTemplates = dashboardTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            var dashboardIds = dashboardTemplates.Select(x => x.Id).ToList();
            List<DashboardItemTemplate> dashboardItemTemplates = dashboardItemTemplateRepo.Queryable.Where(x => dashboardIds.Contains(x.DashboardTemplateId)).ToList();
            List<WfDefinitionTemplate> wfTemplates = wfTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            List<PrintDefTemplate> printTemplateTemplates = printTemplateTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            List<AuthGroupTemplate> authGroupTemplates = authGroupTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();

            var newAppId = appDefRepo.NewId();
            var appDef = new AppDef
            {
                Id = newAppId,
                TemplateId = appTemplate.Id,
                Name = profile.Name,
                Description = profile.Summary,
                Icon = profile.Icon,
                IconColor = profile.ThemeColor,
                AppMenus = []
            };

            await appDefRepo.InsertAsync(appDef);

            var formMap = formTemplates.ToDictionary(x => x.Id, _ => formDefRepo.NewId());
            var dashboardMap = dashboardTemplates.ToDictionary(x => x.Id, _ => dashboardDefRepo.NewId());
            var dashboardLayoutMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dashboardItemMap = dashboardItemTemplates.ToDictionary(x => x.Id, _ => dashboardItemDefRepo.NewId());
            var wfMap = wfTemplates.ToDictionary(x => x.Id, _ => wfDefRepo.NewId());
            var printMap = printTemplateTemplates.ToDictionary(x => x.Id, _ => printTemplateRepo.NewId());
            var authGroupMap = authGroupTemplates.ToDictionary(x => x.Id, _ => authGroupRepo.NewId());

            foreach (var formTemplate in formTemplates)
            {
                var formDef = new FormDef
                {
                    Id = formMap[formTemplate.Id],
                    AppId = newAppId,
                    TemplateId = formTemplate.Id,
                    Name = formTemplate.Name,
                    Content = AppTemplateReferenceRewriter.RewriteFormContent(formTemplate, newAppId, formMap, dashboardMap),
                    FormSettings = AppTemplateReferenceRewriter.RewriteFormSettings(formTemplate, formMap, dashboardMap),
                    IsLedger = formTemplate.IsLedger,
                    UsingWorkflow = formTemplate.UsingWorkflow
                };
                await formDefRepo.InsertAsync(formDef);
            }

            foreach (var dashboardTemplate in dashboardTemplates)
            {
                var layout = AppTemplateReferenceRewriter.RewriteDashboardLayout(dashboardTemplate.Layout, dashboardLayoutMap);
                var dashboardDef = new DashboardDef
                {
                    Id = dashboardMap[dashboardTemplate.Id],
                    AppId = newAppId,
                    TemplateId = dashboardTemplate.Id,
                    Name = dashboardTemplate.Name,
                    Layout = layout
                };
                await dashboardDefRepo.InsertAsync(dashboardDef);
            }

            foreach (var itemTemplate in dashboardItemTemplates)
            {
                var dashboardItem = new DashboardItemDef
                {
                    Id = dashboardItemMap[itemTemplate.Id],
                    AppId = newAppId,
                    DashboardId = dashboardMap[itemTemplate.DashboardTemplateId],
                    TemplateId = itemTemplate.Id,
                    ItemType = itemTemplate.ItemType,
                    LayoutId = dashboardLayoutMap.TryGetValue(itemTemplate.LayoutId, out var layoutId) ? layoutId : itemTemplate.LayoutId,
                    Name = itemTemplate.Name,
                    Details = AppTemplateReferenceRewriter.RewriteJsonReferences(itemTemplate.Details, newAppId, formMap, dashboardMap, wfMap, printMap)
                };
                await dashboardItemDefRepo.InsertAsync(dashboardItem);
            }

            foreach (var wfTemplate in wfTemplates)
            {
                var wf = new Wf_Definition
                {
                    Id = wfMap[wfTemplate.Id],
                    AppId = newAppId,
                    TemplateId = wfTemplate.Id,
                    Name = wfTemplate.Name,
                    FlowType = wfTemplate.FlowType,
                    ExternalId = formMap.TryGetValue(wfTemplate.ExternalTemplateId, out var mappedFormId) ? mappedFormId : wfTemplate.ExternalTemplateId,
                    Description = wfTemplate.Description,
                    Content = AppTemplateReferenceRewriter.RewriteJsonReferences(wfTemplate.Content, newAppId, formMap, dashboardMap, wfMap, printMap),
                    Metadata = AppTemplateReferenceRewriter.RewriteWorkflowMetadata(wfTemplate.Metadata, formMap, dashboardMap, wfMap, printMap),
                    EventSource = wfTemplate.EventSource,
                    SourceId = AppTemplateReferenceRewriter.MapTemplateReference(wfTemplate.SourceTemplateId, formMap, dashboardMap, wfMap),
                    EventSetting = AppTemplateReferenceRewriter.RewriteEventSetting(wfTemplate.EventSetting, formMap, dashboardMap, wfMap),
                    Disabled = wfTemplate.Disabled,
                    IsCurrent = true,
                    Released = false,
                    Version = 1
                };
                await wfDefRepo.InsertAsync(wf);
            }

            foreach (var printTemplateTemplate in printTemplateTemplates)
            {
                var printTemplate = new PrintDef
                {
                    Id = printMap[printTemplateTemplate.Id],
                    AppId = newAppId,
                    TemplateId = printTemplateTemplate.Id,
                    FormId = formMap.TryGetValue(printTemplateTemplate.FormTemplateId, out var mappedFormId) ? mappedFormId : string.Empty,
                    Name = printTemplateTemplate.Name,
                    Content = AppTemplateReferenceRewriter.RewriteJsonReferences(printTemplateTemplate.Content, newAppId, formMap, dashboardMap, wfMap, printMap),
                    PrintType = printTemplateTemplate.PrintType
                };
                await printTemplateRepo.InsertAsync(printTemplate);
            }

            foreach (var authGroupTemplate in authGroupTemplates)
            {
                var authGroup = new AuthGroup
                {
                    Id = authGroupMap[authGroupTemplate.Id],
                    AppId = newAppId,
                    TemplateId = authGroupTemplate.Id,
                    FormId = formMap.TryGetValue(authGroupTemplate.FormTemplateId, out var formDefId) ? formDefId : authGroupTemplate.FormTemplateId,
                    Name = authGroupTemplate.Name,
                    Desc = authGroupTemplate.Desc,
                    Type = authGroupTemplate.Type,
                    DataPerms = authGroupTemplate.DataPerms,
                    DataFilter = authGroupTemplate.DataFilter,
                    FieldPerms = authGroupTemplate.FieldPerms,
                    Disabled = authGroupTemplate.Disabled,
                };
                await authGroupRepo.InsertAsync(authGroup);
            }

            appDef.AppMenus = BuildInstalledMenus(appTemplate, formMap, dashboardMap);
            await appDefRepo.ReplaceAsync(appDef);

            profile.InstallCount += 1;
            profileRepo.Replace(profile);

            return newAppId;
        }

        private static List<AppMenu> BuildInstalledMenus(AppTemplate appTemplate, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap)
        {
            if (string.IsNullOrWhiteSpace(appTemplate.Menus))
            {
                return [];
            }

            var rawMenus = JsonNode.Parse(appTemplate.Menus) as JsonArray;
            if (rawMenus == null)
            {
                return [];
            }

            return rawMenus.Select(node => MapMenu(node, formMap, dashboardMap)).Where(x => x != null).Cast<AppMenu>().ToList();
        }

        private static AppMenu? MapMenu(JsonNode? node, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap)
        {
            if (node is not JsonObject obj)
            {
                return null;
            }

            var menuType = obj["menuType"]?.GetValue<int>() ?? 0;
            var sourceMenuId = obj["menuId"]?.GetValue<string>() ?? string.Empty;
            var menuId = sourceMenuId;
            if (menuType == (int)FormType.Form && formMap.TryGetValue(sourceMenuId, out var formId))
            {
                menuId = formId;
            }
            else if (menuType == (int)FormType.Dashboard && dashboardMap.TryGetValue(sourceMenuId, out var dashboardId))
            {
                menuId = dashboardId;
            }
            else if (menuType == (int)FormType.Group)
            {
                menuId = Guid.NewGuid().ToString("N");
            }

            var subMenus = obj["subMenus"] as JsonArray;
            return new AppMenu
            {
                MenuId = menuId,
                Title = obj["title"]?.GetValue<string>() ?? string.Empty,
                Icon = obj["icon"]?.GetValue<string>() ?? string.Empty,
                IconColor = obj["iconColor"]?.GetValue<string>() ?? string.Empty,
                MenuType = (FormType)menuType,
                SortIndex = obj["sortIndex"]?.GetValue<float>() ?? 0,
                SubMenus = subMenus?.Select(x => MapMenu(x, formMap, dashboardMap)).Where(x => x != null).Cast<AppMenu>().ToList()
            };
        }

    }
}
