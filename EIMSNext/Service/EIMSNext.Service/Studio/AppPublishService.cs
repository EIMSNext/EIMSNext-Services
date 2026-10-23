using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq.Expressions;

using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    public class AppPublishService(IResolver resolver) : IAppPublishService
    {
        private readonly IResolver _resolver = resolver;

        public async Task<AppPublishResult> PublishAsync(string appDefId)
        {
            var warnings = new List<string>();
            var appDefRepo = _resolver.GetRepository<AppDef>();
            var formDefRepo = _resolver.GetRepository<FormDef>();
            var dashboardDefRepo = _resolver.GetRepository<DashboardDef>();
            var dashboardItemDefRepo = _resolver.GetRepository<DashboardItemDef>();
            var wfDefRepo = _resolver.GetRepository<Wf_Definition>();
            var printDefRepo = _resolver.GetRepository<PrintDef>();
            var permissionGroupRepo = _resolver.GetRepository<FormDataPermissionGroup>();
            var permissionGroupTemplateRepo = _resolver.GetRepository<FormDataPermissionGroupTemplate>();
            var appTemplateRepo = _resolver.GetRepository<AppTemplate>();
            var formTemplateRepo = _resolver.GetRepository<FormTemplate>();
            var dashboardTemplateRepo = _resolver.GetRepository<DashboardTemplate>();
            var dashboardItemTemplateRepo = _resolver.GetRepository<DashboardItemTemplate>();
            var wfTemplateRepo = _resolver.GetRepository<WfDefinitionTemplate>();
            var printTemplateRepo = _resolver.GetRepository<PrintDefTemplate>();
            var appProfileRepo = _resolver.GetRepository<AppProfile>();

            var appDef = appDefRepo.Get(appDefId);
            if (appDef == null || appDef.DeleteFlag)
            {
                throw new NotFoundException("应用定义不存在");
            }
            var formDefs = formDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag).ToList();
            var dashboardDefs = dashboardDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag).ToList();
            var dashboardIds = dashboardDefs.Select(x => x.Id).ToList();
            var dashboardItemDefs = dashboardItemDefRepo.Queryable.Where(x => x.AppId == appDefId && dashboardIds.Contains(x.DashboardId) && !x.DeleteFlag).ToList();
            var wfDefs = wfDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag && x.IsCurrent).ToList();
            var printDefs = printDefRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag).ToList();
            var permissionGroups = permissionGroupRepo.Queryable.Where(x => x.AppId == appDefId && !x.DeleteFlag).ToList();

            var appTemplateId = EnsureTemplateId(appTemplateRepo, appDef.TemplateId);
            var formMap = formDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(formTemplateRepo, x.TemplateId));
            var dashboardMap = dashboardDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(dashboardTemplateRepo, x.TemplateId));
            var dashboardItemMap = dashboardItemDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(dashboardItemTemplateRepo, x.TemplateId));
            // 工作流绑定的表单（ExternalId）无法映射时整个工作流跳过发布，原因写入 warnings 供发布人员查看。
            var wfMap = new Dictionary<string, string>();
            var publishableWfDefs = new List<(Wf_Definition Def, string ExternalTemplateId)>();
            foreach (var wfDef in wfDefs)
            {
                var externalTemplateId = MapEntityReferenceToTemplate(
                    wfDef.ExternalId, formMap, dashboardMap, wfMap, warnings,
                    $"工作流[{wfDef.Name}] 绑定的表单不存在或已被删除，已跳过该工作流: {wfDef.ExternalId}");
                if (externalTemplateId == null)
                {
                    continue;
                }

                wfMap[wfDef.Id] = EnsureTemplateId(wfTemplateRepo, wfDef.TemplateId);
                publishableWfDefs.Add((wfDef, externalTemplateId));
            }
            // 打印模板/权限组绑定的表单（FormId）无法映射时整个资源跳过发布，原因写入 warnings 供发布人员查看。
            var publishablePrintDefs = new List<PrintDef>();
            foreach (var printDef in printDefs)
            {
                if (!string.IsNullOrWhiteSpace(printDef.FormId) && !formMap.ContainsKey(printDef.FormId))
                {
                    AddPublishWarning(warnings, $"打印模板[{printDef.Name}] 绑定的表单不存在或已被删除，已跳过该打印模板: {printDef.FormId}");
                    continue;
                }

                publishablePrintDefs.Add(printDef);
            }
            var publishablePermissionGroups = new List<FormDataPermissionGroup>();
            foreach (var permissionGroup in permissionGroups)
            {
                if (!string.IsNullOrWhiteSpace(permissionGroup.FormId) && !formMap.ContainsKey(permissionGroup.FormId))
                {
                    AddPublishWarning(warnings, $"权限组[{permissionGroup.Name}] 绑定的表单不存在或已被删除，已跳过该权限组: {permissionGroup.FormId}");
                    continue;
                }

                publishablePermissionGroups.Add(permissionGroup);
            }
            var printMap = publishablePrintDefs.ToDictionary(x => x.Id, x => EnsureTemplateId(printTemplateRepo, x.TemplateId));
            var permissionGroupMap = publishablePermissionGroups.ToDictionary(x => x.Id, x => EnsureTemplateId(permissionGroupTemplateRepo, x.TemplateId));
            var dashboardLayoutMap = CreateLayoutTemplateMap(dashboardDefs);

            var formTemplateState = GetTemplateState(formTemplateRepo, appTemplateId, formMap.Values, x => x.AppTemplateId);
            var dashboardTemplateState = GetTemplateState(dashboardTemplateRepo, appTemplateId, dashboardMap.Values, x => x.AppTemplateId);
            var dashboardItemTemplateState = GetTemplateState(dashboardItemTemplateRepo, appTemplateId, dashboardItemMap.Values, x => x.AppTemplateId);
            var workflowTemplateState = GetTemplateState(wfTemplateRepo, appTemplateId, wfMap.Values, x => x.AppTemplateId);
            var printTemplateState = GetTemplateState(printTemplateRepo, appTemplateId, printMap.Values, x => x.AppTemplateId);
            var permissionGroupTemplateState = GetTemplateState(permissionGroupTemplateRepo, appTemplateId, permissionGroupMap.Values, x => x.AppTemplateId);
            var appTemplateExists = appTemplateRepo.Get(appTemplateId) != null;
            var profile = appProfileRepo.Queryable.FirstOrDefault(x => x.TemplateId == appTemplateId && !x.DeleteFlag);
            var profileExists = profile != null;
            profile ??= new AppProfile { Id = appProfileRepo.NewId(), TemplateId = appTemplateId };

            var appTemplate = new AppTemplate
            {
                Id = appTemplateId,
                Name = appDef.Name,
                Description = appDef.Description,
                Icon = appDef.Icon,
                Menus = SerializeTemplateMenus(appDef.AppMenus, formMap, dashboardMap, warnings)
            };
            var formTemplates = formDefs.ToDictionary(formDef => formDef.Id, formDef => new FormTemplate
            {
                Id = formMap[formDef.Id],
                AppTemplateId = appTemplateId,
                Name = formDef.Name,
                Type = FormType.Form,
                Icon = string.Empty,
                Content = RewriteFormDefContent(formDef, formMap, dashboardMap, wfMap, printMap, warnings, $"表单[{formDef.Name}]"),
                UsingWorkflow = formDef.UsingWorkflow,
                FormSettings = RewriteFormDefSettings(formDef, formMap, dashboardMap, wfMap, printMap, warnings, $"表单[{formDef.Name}]")
            });
            var dashboardTemplates = dashboardDefs.ToDictionary(dashboardDef => dashboardDef.Id, dashboardDef => new DashboardTemplate
            {
                Id = dashboardMap[dashboardDef.Id],
                AppTemplateId = appTemplateId,
                Name = dashboardDef.Name,
                Layout = RewriteDashboardLayoutToTemplate(dashboardDef.Layout, dashboardLayoutMap)
            });
            var dashboardItemTemplates = dashboardItemDefs.ToDictionary(itemDef => itemDef.Id, itemDef => new DashboardItemTemplate
            {
                Id = dashboardItemMap[itemDef.Id],
                AppTemplateId = appTemplateId,
                DashboardTemplateId = dashboardMap[itemDef.DashboardId],
                ItemType = itemDef.ItemType,
                LayoutId = dashboardLayoutMap.TryGetValue(itemDef.LayoutId, out var layoutId) ? layoutId : itemDef.LayoutId,
                Name = itemDef.Name,
                Details = RewriteJsonToTemplate(itemDef.Details, formMap, dashboardMap, wfMap, printMap, warnings, $"看板项[{itemDef.Name}]")
            });
            var workflowTemplates = publishableWfDefs.ToDictionary(entry => entry.Def.Id, entry => new WfDefinitionTemplate
            {
                Id = wfMap[entry.Def.Id],
                AppTemplateId = appTemplateId,
                Name = entry.Def.Name,
                FlowType = entry.Def.FlowType,
                ExternalTemplateId = entry.ExternalTemplateId,
                Description = entry.Def.Description,
                Content = RewriteJsonToTemplate(entry.Def.Content, formMap, dashboardMap, wfMap, printMap, warnings, $"工作流[{entry.Def.Name}]"),
                Metadata = RewriteWorkflowMetadataToTemplate(entry.Def.Metadata, formMap, dashboardMap, wfMap, printMap, warnings, $"工作流[{entry.Def.Name}]"),
                EventSource = entry.Def.EventSource,
                SourceTemplateId = MapEntityReferenceToTemplate(entry.Def.SourceId, formMap, dashboardMap, wfMap, warnings, $"工作流[{entry.Def.Name}] 的事件来源不存在或已被删除，已忽略事件来源: {entry.Def.SourceId}"),
                EventSetting = RewriteEventSettingToTemplate(entry.Def.EventSetting, formMap, dashboardMap, wfMap, warnings, $"工作流[{entry.Def.Name}]"),
                Disabled = entry.Def.Disabled
            });
            var printTemplates = publishablePrintDefs.ToDictionary(printDef => printDef.Id, printDef => new PrintDefTemplate
            {
                Id = printMap[printDef.Id],
                AppTemplateId = appTemplateId,
                FormTemplateId = formMap.TryGetValue(printDef.FormId, out var formTemplateId) ? formTemplateId : string.Empty,
                Name = printDef.Name,
                Content = RewriteJsonToTemplate(printDef.Content, formMap, dashboardMap, wfMap, printMap, warnings, $"打印模板[{printDef.Name}]"),
                PrintType = printDef.PrintType
            });
            var permissionGroupTemplates = publishablePermissionGroups.ToDictionary(permissionGroup => permissionGroup.Id, permissionGroup => new FormDataPermissionGroupTemplate
            {
                Id = permissionGroupMap[permissionGroup.Id],
                AppTemplateId = appTemplateId,
                FormTemplateId = formMap.TryGetValue(permissionGroup.FormId, out var groupFormTemplateId) ? groupFormTemplateId : permissionGroup.FormId,
                Name = permissionGroup.Name,
                Desc = permissionGroup.Desc,
                Type = permissionGroup.Type,
                FormDataPermissions = permissionGroup.FormDataPermissions,
                DataFilter = permissionGroup.DataFilter,
                FormFieldPermissions = permissionGroup.FormFieldPermissions,
                Disabled = permissionGroup.Disabled,
            });
            profile.Name = appDef.Name;
            profile.Summary = string.IsNullOrWhiteSpace(appDef.Description) ? appDef.Name : appDef.Description;
            profile.Description = appDef.Description;
            profile.Icon = appDef.Icon;
            profile.ThemeColor = appDef.IconColor;
            profile.Status = AppProfileStatus.Published;
            profile.PublishedAt ??= DateTime.UtcNow;

            using var scope = appDefRepo.NewTransactionScope();

            await DeleteTemplatesAsync(formTemplateRepo, formTemplateState.StaleIds);
            await DeleteTemplatesAsync(dashboardTemplateRepo, dashboardTemplateState.StaleIds);
            await DeleteTemplatesAsync(dashboardItemTemplateRepo, dashboardItemTemplateState.StaleIds);
            await DeleteTemplatesAsync(wfTemplateRepo, workflowTemplateState.StaleIds);
            await DeleteTemplatesAsync(printTemplateRepo, printTemplateState.StaleIds);
            await DeleteTemplatesAsync(permissionGroupTemplateRepo, permissionGroupTemplateState.StaleIds);

            await UpsertAsync(appTemplateRepo, appTemplate, appTemplateExists);

            foreach (var formDef in formDefs)
            {
                await UpsertAsync(formTemplateRepo, formTemplates[formDef.Id], formTemplateState.ExistingIds.Contains(formMap[formDef.Id]));

                await SetTemplateIdAsync(formDefRepo, formDef, formMap[formDef.Id]);
            }

            foreach (var dashboardDef in dashboardDefs)
            {
                await UpsertAsync(dashboardTemplateRepo, dashboardTemplates[dashboardDef.Id], dashboardTemplateState.ExistingIds.Contains(dashboardMap[dashboardDef.Id]));

                await SetTemplateIdAsync(dashboardDefRepo, dashboardDef, dashboardMap[dashboardDef.Id]);
            }

            foreach (var itemDef in dashboardItemDefs)
            {
                await UpsertAsync(dashboardItemTemplateRepo, dashboardItemTemplates[itemDef.Id], dashboardItemTemplateState.ExistingIds.Contains(dashboardItemMap[itemDef.Id]));

                await SetTemplateIdAsync(dashboardItemDefRepo, itemDef, dashboardItemMap[itemDef.Id]);
            }

            foreach (var (wfDef, _) in publishableWfDefs)
            {
                await UpsertAsync(wfTemplateRepo, workflowTemplates[wfDef.Id], workflowTemplateState.ExistingIds.Contains(wfMap[wfDef.Id]));

                await SetTemplateIdAsync(wfDefRepo, wfDef, wfMap[wfDef.Id]);
            }

            foreach (var printDef in publishablePrintDefs)
            {
                await UpsertAsync(printTemplateRepo, printTemplates[printDef.Id], printTemplateState.ExistingIds.Contains(printMap[printDef.Id]));

                await SetTemplateIdAsync(printDefRepo, printDef, printMap[printDef.Id]);
            }

            foreach (var permissionGroup in publishablePermissionGroups)
            {
                await UpsertAsync(permissionGroupTemplateRepo, permissionGroupTemplates[permissionGroup.Id], permissionGroupTemplateState.ExistingIds.Contains(permissionGroupMap[permissionGroup.Id]));

                await SetTemplateIdAsync(permissionGroupRepo, permissionGroup, permissionGroupMap[permissionGroup.Id]);
            }

            await SetTemplateIdAsync(appDefRepo, appDef, appTemplateId);

            await UpsertAsync(appProfileRepo, profile, profileExists);

            scope.CommitTransaction();

            return new AppPublishResult(appTemplateId, warnings);
        }

        private static TemplateState GetTemplateState<T>(
            IRepository<T> repo,
            string appTemplateId,
            IEnumerable<string> currentIds,
            Expression<Func<T, string>> appTemplateIdSelector)
            where T : class, IEntityKey
        {
            var keepIds = currentIds.ToHashSet(StringComparer.Ordinal);
            var existingIds = repo.Queryable
                .Where(Expression.Lambda<Func<T, bool>>(
                    Expression.Equal(appTemplateIdSelector.Body, Expression.Constant(appTemplateId)),
                    appTemplateIdSelector.Parameters))
                .Select(x => x.Id)
                .ToHashSet(StringComparer.Ordinal);
            return new TemplateState(existingIds, existingIds.Where(id => !keepIds.Contains(id)).ToList());
        }

        private static async Task DeleteTemplatesAsync<T>(IRepository<T> repo, IReadOnlyCollection<string> staleIds)
            where T : class, IEntityKey
        {
            if (staleIds.Count > 0)
            {
                await repo.DeleteAsync(staleIds);
            }
        }

        private static string EnsureTemplateId<T>(IRepository<T> repo, string? templateId) where T : class, EIMSNext.Core.Abstractions.IEntityKey
        {
            return string.IsNullOrWhiteSpace(templateId) ? repo.NewId() : templateId;
        }

        private static async Task UpsertAsync<T>(IRepository<T> repo, T entity, bool exists) where T : class, EIMSNext.Core.Abstractions.IEntityKey
        {
            if (!exists)
            {
                await repo.InsertAsync(entity);
                return;
            }

            await repo.ReplaceAsync(entity);
        }

        private sealed record TemplateState(HashSet<string> ExistingIds, List<string> StaleIds);

        private static async Task SetTemplateIdAsync<T>(IRepository<T> repo, T entity, string templateId) where T : class, EIMSNext.Core.Abstractions.IEntityKey
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
                case FormDataPermissionGroup auth:
                    auth.TemplateId = templateId;
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
                            layoutMap[layoutId] = TsidIdGenerator.NewId();
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
                    if (obj["parentLayoutId"] is JsonValue parentValue)
                    {
                        var parentLayoutId = parentValue.GetValue<string>();
                        if (layoutMap.TryGetValue(parentLayoutId, out var templateParentLayoutId))
                        {
                            obj["parentLayoutId"] = templateParentLayoutId;
                        }
                    }

                    foreach (var property in obj.ToList())
                    {
                        RewriteLayoutNodeToTemplate(property.Value, layoutMap);
                    }
                    break;
            }
        }

        private static FormContent RewriteFormDefContent(FormDef formDef, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string> printMap, List<string> warnings, string ownerLabel)
        {
            var json = JsonSerializer.Serialize(formDef.Content);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, printMap, warnings, ownerLabel);
            return JsonSerializer.Deserialize<FormContent>(rewritten) ?? new FormContent();
        }

        private static FormSettings RewriteFormDefSettings(FormDef formDef, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string> printMap, List<string> warnings, string ownerLabel)
        {
            var json = JsonSerializer.Serialize(formDef.FormSettings);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, printMap, warnings, ownerLabel);
            return JsonSerializer.Deserialize<FormSettings>(rewritten) ?? new FormSettings();
        }

        private static WfMetadata RewriteWorkflowMetadataToTemplate(WfMetadata metadata, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string> printMap, List<string> warnings, string ownerLabel)
        {
            var json = JsonSerializer.Serialize(metadata);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, printMap, warnings, ownerLabel);
            return JsonSerializer.Deserialize<WfMetadata>(rewritten) ?? new WfMetadata();
        }

        private static EventSetting? RewriteEventSettingToTemplate(EventSetting? eventSetting, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, List<string> warnings, string ownerLabel)
        {
            if (eventSetting == null)
            {
                return null;
            }

            var json = JsonSerializer.Serialize(eventSetting);
            var rewritten = RewriteJsonToTemplate(json, formMap, dashboardMap, workflowMap, null, warnings, ownerLabel);
            return JsonSerializer.Deserialize<EventSetting>(rewritten);
        }

        private static string RewriteJsonToTemplate(string json, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, Dictionary<string, string>? printMap, List<string> warnings, string ownerLabel)
        {
            return AppTemplateReferenceRewriter.RewriteJsonReferences(
                json,
                string.Empty,
                formMap,
                dashboardMap,
                workflowMap,
                printMap,
                warnings,
                ownerLabel);
        }

        /// <summary>
        /// 将发布引用映射为模板 Id；映射不到时不中止发布，返回 null 并把原因写入 <paramref name="warnings"/>。
        /// </summary>
        private static string? MapEntityReferenceToTemplate(string? entityId, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, Dictionary<string, string> workflowMap, List<string> warnings, string unmappedMessage)
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

            if (!warnings.Contains(unmappedMessage))
            {
                warnings.Add(unmappedMessage);
            }

            return null;
        }

        private static string SerializeTemplateMenus(List<AppMenu> menus, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, List<string> warnings)
        {
            var items = new JsonArray();
            foreach (var menu in menus)
            {
                var mappedMenu = MapMenuToTemplate(menu, formMap, dashboardMap, warnings);
                if (mappedMenu != null)
                {
                    items.Add(mappedMenu);
                }
            }

            return items.ToJsonString();
        }

        private static JsonObject? MapMenuToTemplate(AppMenu menu, Dictionary<string, string> formMap, Dictionary<string, string> dashboardMap, List<string> warnings)
        {
            var menuId = menu.MenuId;
            if (menu.MenuType == FormType.Form)
            {
                if (!formMap.TryGetValue(menu.MenuId, out var formTemplateId))
                {
                    AddPublishWarning(warnings, $"菜单[{menu.Title}] 引用的表单不存在或已被删除，已跳过该菜单: {menu.MenuId}");
                    return null;
                }
                menuId = formTemplateId;
            }
            else if (menu.MenuType == FormType.Dashboard)
            {
                if (!dashboardMap.TryGetValue(menu.MenuId, out var dashboardTemplateId))
                {
                    AddPublishWarning(warnings, $"菜单[{menu.Title}] 引用的看板不存在或已被删除，已跳过该菜单: {menu.MenuId}");
                    return null;
                }
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
                ["editable"] = menu.Editable,
                ["deletable"] = menu.Deletable,
                ["listComponent"] = menu.ListComponent,
            };

            if (menu.SubMenus?.Count > 0)
            {
                var subMenus = new JsonArray();
                foreach (var subMenu in menu.SubMenus)
                {
                    var mappedSubMenu = MapMenuToTemplate(subMenu, formMap, dashboardMap, warnings);
                    if (mappedSubMenu != null)
                    {
                        subMenus.Add(mappedSubMenu);
                    }
                }
                obj["subMenus"] = subMenus;
            }

            return obj;
        }

        private static void AddPublishWarning(List<string> warnings, string message)
        {
            if (!warnings.Contains(message))
            {
                warnings.Add(message);
            }
        }
    }
}
