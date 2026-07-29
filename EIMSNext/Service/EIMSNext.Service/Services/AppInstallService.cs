using System.Text.Json.Nodes;
using System.Text.Json;
using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
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

            var profile = profileRepo.Get(appProfileId) ?? throw new NotFoundException("应用档案不存在");
            if (profile.DeleteFlag || profile.Status != AppProfileStatus.Published)
            {
                throw new NotFoundException("应用已下架或不存在");
            }

            var appTemplate = appTemplateRepo.Get(profile.TemplateId) ?? throw new NotFoundException("应用模板不存在");
            var context = _resolver.GetServiceContext();
            if (string.IsNullOrWhiteSpace(context.CorpId))
            {
                throw new BadRequestException("当前用户未选择企业，无法安装应用");
            }

            List<FormTemplate> formTemplates = formTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            List<DashboardTemplate> dashboardTemplates = dashboardTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            var dashboardIds = dashboardTemplates.Select(x => x.Id).ToList();
            List<DashboardItemTemplate> dashboardItemTemplates = dashboardItemTemplateRepo.Queryable.Where(x => dashboardIds.Contains(x.DashboardTemplateId)).ToList();
            List<WfDefinitionTemplate> wfTemplates = wfTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            List<PrintDefTemplate> printTemplateTemplates = printTemplateTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();
            List<AuthGroupTemplate> authGroupTemplates = authGroupTemplateRepo.Queryable.Where(x => x.AppTemplateId == appTemplate.Id).ToList();

            var now = DateTime.UtcNow.ToTimeStampMs();
            var newAppId = appDefRepo.NewId();
            var formMap = formTemplates.ToDictionary(x => x.Id, _ => formDefRepo.NewId());
            var dashboardMap = dashboardTemplates.ToDictionary(x => x.Id, _ => dashboardDefRepo.NewId());
            var dashboardLayoutMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dashboardItemMap = dashboardItemTemplates.ToDictionary(x => x.Id, _ => dashboardItemDefRepo.NewId());
            var wfMap = wfTemplates.ToDictionary(x => x.Id, _ => wfDefRepo.NewId());
            var printMap = printTemplateTemplates.ToDictionary(x => x.Id, _ => printTemplateRepo.NewId());
            var authGroupMap = authGroupTemplates.ToDictionary(x => x.Id, _ => authGroupRepo.NewId());

            var appDef = InitializeInstalledEntity(new AppDef
            {
                Id = newAppId,
                TemplateId = appTemplate.Id,
                Name = profile.Name,
                Description = profile.Summary,
                Icon = profile.Icon,
                IconColor = profile.ThemeColor,
                AppMenus = BuildInstalledMenus(appTemplate, formMap, dashboardMap)
            }, context, now);

            var formDefs = formTemplates.Select(formTemplate => InitializeInstalledEntity(new FormDef
            {
                Id = formMap[formTemplate.Id],
                AppId = newAppId,
                TemplateId = formTemplate.Id,
                Name = formTemplate.Name,
                Content = AppTemplateReferenceRewriter.RewriteFormContent(formTemplate, newAppId, formMap, dashboardMap),
                FormSettings = AppTemplateReferenceRewriter.RewriteFormSettings(formTemplate, formMap, dashboardMap),
                UsingWorkflow = formTemplate.UsingWorkflow
            }, context, now)).ToList();
            foreach (var formDef in formDefs)
            {
                formDef.PublicRelatedFormIds = FormRelatedSourceResolver.ResolveFormIds(formDef.Content.Layout).ToList();
            }

            var dashboardDefs = new List<DashboardDef>();
            foreach (var dashboardTemplate in dashboardTemplates)
            {
                dashboardDefs.Add(InitializeInstalledEntity(new DashboardDef
                {
                    Id = dashboardMap[dashboardTemplate.Id],
                    AppId = newAppId,
                    TemplateId = dashboardTemplate.Id,
                    Name = dashboardTemplate.Name,
                    Layout = AppTemplateReferenceRewriter.RewriteDashboardLayout(dashboardTemplate.Layout, dashboardLayoutMap)
                }, context, now));
            }

            var dashboardItemDefs = dashboardItemTemplates.Select(itemTemplate => InitializeInstalledEntity(new DashboardItemDef
            {
                Id = dashboardItemMap[itemTemplate.Id],
                AppId = newAppId,
                DashboardId = dashboardMap[itemTemplate.DashboardTemplateId],
                TemplateId = itemTemplate.Id,
                ItemType = itemTemplate.ItemType,
                LayoutId = dashboardLayoutMap.TryGetValue(itemTemplate.LayoutId, out var layoutId) ? layoutId : itemTemplate.LayoutId,
                Name = itemTemplate.Name,
                Details = AppTemplateReferenceRewriter.RewriteJsonReferences(itemTemplate.Details, newAppId, formMap, dashboardMap, wfMap, printMap)
            }, context, now)).ToList();

            var wfDefs = wfTemplates.Select(wfTemplate => InitializeInstalledEntity(new Wf_Definition
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
            }, context, now)).ToList();

            var printDefs = printTemplateTemplates.Select(printTemplate => InitializeInstalledEntity(new PrintDef
            {
                Id = printMap[printTemplate.Id],
                AppId = newAppId,
                TemplateId = printTemplate.Id,
                FormId = formMap.TryGetValue(printTemplate.FormTemplateId, out var mappedFormId) ? mappedFormId : string.Empty,
                Name = printTemplate.Name,
                Content = AppTemplateReferenceRewriter.RewriteJsonReferences(printTemplate.Content, newAppId, formMap, dashboardMap, wfMap, printMap),
                PrintType = printTemplate.PrintType
            }, context, now)).ToList();

            var authGroups = authGroupTemplates.Select(authGroupTemplate => InitializeInstalledEntity(new AuthGroup
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
            }, context, now)).ToList();

            using var scope = appDefRepo.NewTransactionScope();
            await appDefRepo.InsertAsync(appDef);
            if (formDefs.Count > 0) await formDefRepo.InsertAsync(formDefs);
            if (dashboardDefs.Count > 0) await dashboardDefRepo.InsertAsync(dashboardDefs);
            if (dashboardItemDefs.Count > 0) await dashboardItemDefRepo.InsertAsync(dashboardItemDefs);
            if (wfDefs.Count > 0) await wfDefRepo.InsertAsync(wfDefs);
            if (printDefs.Count > 0) await printTemplateRepo.InsertAsync(printDefs);
            if (authGroups.Count > 0) await authGroupRepo.InsertAsync(authGroups);

            await profileRepo.UpdateAsync(
                profile.Id,
                profileRepo.UpdateBuilder.Inc(x => x.InstallCount, 1),
                upsert: false);

            scope.CommitTransaction();

            return newAppId;
        }

        private static T InitializeInstalledEntity<T>(T entity, IServiceContext context, long now) where T : CorpEntityBase
        {
            entity.CorpId = context.CorpId;
            entity.CreateBy = context.Operator;
            entity.UpdateBy = context.Operator;
            entity.CreateTime = now;
            entity.UpdateTime = now;
            return entity;
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
                Editable = obj["editable"]?.GetValue<bool>() ?? true,
                Deletable = obj["deletable"]?.GetValue<bool>() ?? true,
                ListComponent = obj["listComponent"]?.GetValue<string>() ?? string.Empty,
                SubMenus = subMenus?.Select(x => MapMenu(x, formMap, dashboardMap)).Where(x => x != null).Cast<AppMenu>().ToList()
            };
        }

    }
}
