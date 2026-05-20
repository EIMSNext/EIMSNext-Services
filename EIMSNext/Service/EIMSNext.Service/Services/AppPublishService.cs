using System.Text.Json;
using System.Text.Json.Nodes;

using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class AppPublishService(IResolver resolver) : IAppPublishService
    {
        private readonly IResolver _resolver = resolver;

        public async Task<string> PublishAsync(string appDefId)
        {
            var appDefRepo = _resolver.GetRepository<AppDef>();
            var formDefRepo = _resolver.GetRepository<FormDef>();
            var dashboardDefRepo = _resolver.GetRepository<DashboardDef>();
            var dashboardItemDefRepo = _resolver.GetRepository<DashboardItemDef>();
            var wfDefRepo = _resolver.GetRepository<Wf_Definition>();
            var printDefRepo = _resolver.GetRepository<PrintDef>();
            var appTemplateRepo = _resolver.GetRepository<AppTemplate>();
            var formTemplateRepo = _resolver.GetRepository<FormTemplate>();
            var dashboardTemplateRepo = _resolver.GetRepository<DashboardTemplate>();
            var dashboardItemTemplateRepo = _resolver.GetRepository<DashboardItemTemplate>();
            var wfTemplateRepo = _resolver.GetRepository<WfDefinitionTemplate>();
            var printTemplateRepo = _resolver.GetRepository<PrintDefTemplate>();
            var appProfileRepo = _resolver.GetRepository<AppProfile>();

            var appDef = appDefRepo.Get(appDefId) ?? throw new InvalidOperationException("应用定义不存在");
            var formDefs = formDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag).ToList();
            var dashboardDefs = dashboardDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag).ToList();
            var dashboardIds = dashboardDefs.Select(x => x.Id).ToList();
            var dashboardItemDefs = dashboardItemDefRepo.Queryable.Where(x => x.AppId == appDefId && dashboardIds.Contains(x.DashboardId) && !x.DeleteFlag).ToList();
            var wfDefs = wfDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag && x.IsCurrent).ToList();
            var printDefs = printDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag).ToList();

            var appTemplateId = EnsureTemplateId(appTemplateRepo, appDef.TemplateId);
            var formMap = formDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(formTemplateRepo, x.TemplateId));
            var dashboardMap = dashboardDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(dashboardTemplateRepo, x.TemplateId));
            var dashboardItemMap = dashboardItemDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(dashboardItemTemplateRepo, x.TemplateId));
            var wfMap = wfDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(wfTemplateRepo, x.TemplateId));
            var printMap = printDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(printTemplateRepo, x.TemplateId));
            var dashboardLayoutMap = CreateLayoutTemplateMap(dashboardDefs);

            await UpsertAsync(appTemplateRepo, new AppTemplate
            {
                Id = appTemplateId,
                Name = appDef.Name,
                Description = appDef.Description,
                Icon = appDef.Icon,
                Menus = SerializeTemplateMenus(appDef.AppMenus, formMap, dashboardMap)
            });

            foreach (var formDef in formDefs)
            {
                await UpsertAsync(formTemplateRepo, new FormTemplate
                {
                    Id = formMap[formDef.Id],
                    AppTemplateId = appTemplateId,
                    Name = formDef.Name,
                    Type = FormType.Form,
                    Icon = string.Empty,
                    Content = RewriteFormDefContent(formDef, formMap, dashboardMap, wfMap, printMap),
                    IsLedger = formDef.IsLedger,
                    UsingWorkflow = formDef.UsingWorkflow,
                    FormSettings = RewriteFormDefSettings(formDef, formMap, dashboardMap, wfMap, printMap)
                });

                await SetTemplateIdAsync(formDefRepo, formDef, formMap[formDef.Id]);
            }

            foreach (var dashboardDef in dashboardDefs)
            {
                await UpsertAsync(dashboardTemplateRepo, new DashboardTemplate
                {
                    Id = dashboardMap[dashboardDef.Id],
                    AppTemplateId = appTemplateId,
                    Name = dashboardDef.Name,
                    Layout = RewriteDashboardLayoutToTemplate(dashboardDef.Layout, dashboardLayoutMap)
                });

                await SetTemplateIdAsync(dashboardDefRepo, dashboardDef, dashboardMap[dashboardDef.Id]);
            }

            foreach (var itemDef in dashboardItemDefs)
            {
                await UpsertAsync(dashboardItemTemplateRepo, new DashboardItemTemplate
                {
                    Id = dashboardItemMap[itemDef.Id],
                    AppTemplateId = appTemplateId,
                    DashboardTemplateId = dashboardMap[itemDef.DashboardId],
                    ItemType = itemDef.ItemType,
                    LayoutId = dashboardLayoutMap.TryGetValue(itemDef.LayoutId, out var layoutId) ? layoutId : itemDef.LayoutId,
                    Name = itemDef.Name,
                    Details = RewriteJsonToTemplate(itemDef.Details, formMap, dashboardMap, wfMap, printMap)
                });

                await SetTemplateIdAsync(dashboardItemDefRepo, itemDef, dashboardItemMap[itemDef.Id]);
            }

            foreach (var wfDef in wfDefs)
            {
                await UpsertAsync(wfTemplateRepo, new WfDefinitionTemplate
                {
                    Id = wfMap[wfDef.Id],
                    AppTemplateId = appTemplateId,
                    Name = wfDef.Name,
                    FlowType = wfDef.FlowType,
                    ExternalTemplateId = formMap.TryGetValue(wfDef.ExternalId, out var formTemplateId) ? formTemplateId : wfDef.ExternalId,
                    Description = wfDef.Description,
                    Content = RewriteJsonToTemplate(wfDef.Content, formMap, dashboardMap, wfMap, printMap),
                    Metadata = RewriteWorkflowMetadataToTemplate(wfDef.Metadata, formMap, dashboardMap, wfMap, printMap),
                    EventSource = wfDef.EventSource,
                    SourceTemplateId = MapEntityReferenceToTemplate(wfDef.SourceId, formMap, dashboardMap, wfMap),
                    EventSetting = RewriteEventSettingToTemplate(wfDef.EventSetting, formMap, dashboardMap, wfMap),
                    Disabled = wfDef.Disabled
                });

                await SetTemplateIdAsync(wfDefRepo, wfDef, wfMap[wfDef.Id]);
            }

            foreach (var printDef in printDefs)
            {
                await UpsertAsync(printTemplateRepo, new PrintDefTemplate
                {
                    Id = printMap[printDef.Id],
                    AppTemplateId = appTemplateId,
                    FormTemplateId = formMap.TryGetValue(printDef.FormId, out var formTemplateId) ? formTemplateId : string.Empty,
                    Name = printDef.Name,
                    Content = RewriteJsonToTemplate(printDef.Content, formMap, dashboardMap, wfMap, printMap),
                    PrintType = printDef.PrintType
                });

                await SetTemplateIdAsync(printDefRepo, printDef, printMap[printDef.Id]);
            }

            await SetTemplateIdAsync(appDefRepo, appDef, appTemplateId);

            var profile = appProfileRepo.Queryable.FirstOrDefault(x => x.TemplateId == appTemplateId && !x.DeleteFlag) ?? new AppProfile { Id = appProfileRepo.NewId(), TemplateId = appTemplateId };
            profile.Name = appDef.Name;
            profile.Summary = string.IsNullOrWhiteSpace(appDef.Description) ? appDef.Name : appDef.Description;
            profile.Description = appDef.Description;
            profile.Icon = appDef.Icon;
            profile.ThemeColor = appDef.IconColor;
            profile.Status = "Published";
            profile.PublishedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(profile.Author))
            {
                profile.Author = "EIMSNext";
            }

            await UpsertAsync(appProfileRepo, profile);

            return appTemplateId;
        }

        private static string EnsureTemplateId<T>(IRepository<T> repo, string? templateId) where T : class, EIMSNext.Core.Entities.IMongoEntity
        {
            return string.IsNullOrWhiteSpace(templateId) ? repo.NewId() : templateId;
        }

        private static async Task UpsertAsync<T>(IRepository<T> repo, T entity) where T : class, EIMSNext.Core.Entities.IMongoEntity
        {
            if (repo.Get(entity.Id) == null)
            {
                await repo.InsertAsync(entity);
                return;
            }

            await repo.ReplaceAsync(entity);
        }

        private static async Task SetTemplateIdAsync<T>(IRepository<T> repo, T entity, string templateId) where T : class, EIMSNext.Core.Entities.IMongoEntity
        {
            switch (entity)
            {
                case AppDef app:
                    app.TemplateId = templateId;
                    await repo.ReplaceAsync(entity);
                    break;
                case FormDef form:
                    form.TemplateId = templateId;
                    await repo.ReplaceAsync(entity);
                    break;
                case DashboardDef dashboard:
                    dashboard.TemplateId = templateId;
                    await repo.ReplaceAsync(entity);
                    break;
                case DashboardItemDef item:
                    item.TemplateId = templateId;
                    await repo.ReplaceAsync(entity);
                    break;
                case Wf_Definition wf:
                    wf.TemplateId = templateId;
                    await repo.ReplaceAsync(entity);
                    break;
                case PrintDef print:
                    print.TemplateId = templateId;
                    await repo.ReplaceAsync(entity);
                    break;
            }
        }

        private static Dictionary<string, string> CreateLayoutTemplateMap(List<DashboardDef> dashboardDefs)
        {
            var layoutMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dashboard in dashboardDefs)
            {
                var node = JsonNode.Parse(string.IsNullOrWhiteSpace(dashboard.Layout) ? "[]" : dashboard.Layout);
                CollectLayoutIds(node, layoutMap);
            }

            return layoutMap;
        }

        private static void CollectLayoutIds(JsonNode? node, Dictionary<string, string> layoutMap)
        {
            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array)
                    {
                        CollectLayoutIds(item, layoutMap);
                    }
                    break;
                case JsonObject obj:
                    if (obj["i"] is JsonValue idValue)
                    {
                        var layoutId = idValue.GetValue<string>();
                        if (!layoutMap.ContainsKey(layoutId))
                        {
                            layoutMap[layoutId] = Guid.NewGuid().ToString("N");
                        }
                    }

                    foreach (var property in obj.ToList())
                    {
                        CollectLayoutIds(property.Value, layoutMap);
                    }
                    break;
            }
        }

        private static string RewriteDashboardLayoutToTemplate(string layout, Dictionary<string, string> layoutMap)
        {
            if (string.IsNullOrWhiteSpace(layout))
            {
                return "[]";
            }

            var node = JsonNode.Parse(layout);
            RewriteLayoutNodeToTemplate(node, layoutMap);
            return node?.ToJsonString() ?? "[]";
        }

        private static void RewriteLayoutNodeToTemplate(JsonNode? node, Dictionary<string, string> layoutMap)
        {
            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array)
                    {
                        RewriteLayoutNodeToTemplate(item, layoutMap);
                    }
                    break;
                case JsonObject obj:
                    if (obj["i"] is JsonValue idValue)
                    {
                        var layoutId = idValue.GetValue<string>();
                        if (layoutMap.TryGetValue(layoutId, out var templateLayoutId))
                        {
                            obj["i"] = templateLayoutId;
                        }
                    }

                    foreach (var property in obj.ToList())
                    {
                        RewriteLayoutNodeToTemplate(property.Value, layoutMap);
                    }
                    break;
            }
        }

        private static FormContent RewriteFormDefContent(FormDef formDef, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string> printMap)
        {
            var json = JsonSerializer.Serialize(formDef.Content);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, printMap);
            return JsonSerializer.Deserialize<FormContent>(rewritten) ?? new FormContent();
        }

        private static FormSettings RewriteFormDefSettings(FormDef formDef, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string> printMap)
        {
            var json = JsonSerializer.Serialize(formDef.FormSettings);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, printMap);
            return JsonSerializer.Deserialize<FormSettings>(rewritten) ?? new FormSettings();
        }

        private static WfMetadata RewriteWorkflowMetadataToTemplate(WfMetadata metadata, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string> printMap)
        {
            var json = JsonSerializer.Serialize(metadata);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, printMap);
            return JsonSerializer.Deserialize<WfMetadata>(rewritten) ?? new WfMetadata();
        }

        private static EventSetting? RewriteEventSettingToTemplate(EventSetting? eventSetting, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap)
        {
            if (eventSetting == null)
            {
                return null;
            }

            var json = JsonSerializer.Serialize(eventSetting);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, null);
            return JsonSerializer.Deserialize<EventSetting>(rewritten);
        }

        private static string RewriteJsonToTemplate(string json, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string>? printMap)
        {
            return AppTemplateReferenceRewriter.RewriteJsonReferences(
                json,
                string.Empty,
                formMap,
                dashboardMap,
                workflowMap,
                printMap);
        }

        private static string? MapEntityReferenceToTemplate(string? entityId, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap)
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return entityId;
            }

            if (formMap.TryGetValue(entityId, out var formTemplateId))
            {
                return formTemplateId;
            }

            if (dashboardMap.TryGetValue(entityId, out var dashboardTemplateId))
            {
                return dashboardTemplateId;
            }

            if (workflowMap.TryGetValue(entityId, out var workflowTemplateId))
            {
                return workflowTemplateId;
            }

            return entityId;
        }

        private static string SerializeTemplateMenus(List<AppMenu> menus, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap)
        {
            var items = new JsonArray();
            foreach (var menu in menus)
            {
                items.Add(MapMenuToTemplate(menu, formMap, dashboardMap));
            }

            return items.ToJsonString();
        }

        private static JsonObject MapMenuToTemplate(AppMenu menu, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap)
        {
            var menuId = menu.MenuId;
            if (menu.MenuType == FormType.Form && formMap.TryGetValue(menu.MenuId, out var formTemplateId))
            {
                menuId = formTemplateId;
            }
            else if (menu.MenuType == FormType.Dashboard && dashboardMap.TryGetValue(menu.MenuId, out var dashboardTemplateId))
            {
                menuId = dashboardTemplateId;
            }

            var obj = new JsonObject
            {
                ["menuId"] = menuId,
                ["title"] = menu.Title,
                ["icon"] = menu.Icon,
                ["iconColor"] = menu.IconColor,
                ["menuType"] = (int)menu.MenuType,
                ["sortIndex"] = menu.SortIndex,
            };

            if (menu.SubMenus?.Count > 0)
            {
                var subMenus = new JsonArray();
                foreach (var subMenu in menu.SubMenus)
                {
                    subMenus.Add(MapMenuToTemplate(subMenu, formMap, dashboardMap));
                }
                obj["subMenus"] = subMenus;
            }

            return obj;
        }
    }
}
