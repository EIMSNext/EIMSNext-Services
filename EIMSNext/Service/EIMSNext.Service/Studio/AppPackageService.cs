using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.Service
{
    /// <summary>
    /// Handles portable application marketplace template packages. The package always contains
    /// template-layer IDs, so importing never invokes the publish/install reference rewriter.
    /// </summary>
    public class AppPackageService(IResolver resolver) : IAppPackageService
    {
        private const int MaxPackageBytes = 10 * 1024 * 1024;
        private const string ManifestName = "manifest.json";
        private readonly IResolver _resolver = resolver;
        private static readonly JsonSerializerOptions PackageJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        public async Task<AppPackageExport> ExportAsync(string appProfileId)
        {
            if (string.IsNullOrWhiteSpace(appProfileId))
            {
                throw new BadRequestException("appProfileId 不能为空");
            }

            var profileRepo = _resolver.GetRepository<AppProfile>();
            var profile = profileRepo.Get(appProfileId);
            if (profile == null || profile.DeleteFlag)
            {
                throw new NotFoundException("应用市场档案不存在");
            }

            var manifest = BuildManifest(profile);
            ValidateManifest(manifest);

            await using var buffer = new MemoryStream();
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await JsonSerializer.SerializeAsync(entryStream, manifest, PackageJsonOptions);
            }

            if (buffer.Length > MaxPackageBytes)
            {
                throw new BadRequestException("应用模板包超过 10 MB 限制");
            }

            return new AppPackageExport
            {
                FileName = $"{SanitizeFileName(profile.Name)}-{profile.Id}.eimsapp",
                Content = buffer.ToArray(),
            };
        }

        public Task<AppPackagePreview> PreviewAsync(Stream stream, long fileLength)
        {
            var manifest = ReadManifest(stream, fileLength);
            ValidateManifest(manifest);
            var plan = BuildImportPlan(manifest);
            return Task.FromResult(ToPreview(plan));
        }

        public async Task<AppPackageImportResult> ImportAsync(Stream stream, long fileLength)
        {
            var manifest = ReadManifest(stream, fileLength);
            ValidateManifest(manifest);
            var plan = BuildImportPlan(manifest);

            var appTemplateRepo = _resolver.GetRepository<AppTemplate>();
            var formRepo = _resolver.GetRepository<FormTemplate>();
            var dashboardRepo = _resolver.GetRepository<DashboardTemplate>();
            var dashboardItemRepo = _resolver.GetRepository<DashboardItemTemplate>();
            var workflowRepo = _resolver.GetRepository<WfDefinitionTemplate>();
            var printRepo = _resolver.GetRepository<PrintDefTemplate>();
            var permissionGroupRepo = _resolver.GetRepository<FormDataPermissionGroupTemplate>();
            var profileRepo = _resolver.GetRepository<AppProfile>();
            var context = _resolver.GetServiceContext();
            var now = DateTime.UtcNow.ToTimeStampMs();

            using var scope = appTemplateRepo.NewTransactionScope();

            if (!plan.ProfileExists)
            {
                StampForWrite(manifest.Profile, null, context, now);
                await profileRepo.InsertAsync(manifest.Profile);
            }

            await ReplaceAsync(appTemplateRepo, manifest.Template, context, now);
            await ReplaceManyAsync(formRepo, manifest.Forms, context, now);
            await ReplaceManyAsync(dashboardRepo, manifest.Dashboards, context, now);
            await ReplaceManyAsync(dashboardItemRepo, manifest.DashboardItems, context, now);
            await ReplaceManyAsync(workflowRepo, manifest.Workflows, context, now);
            await ReplaceManyAsync(printRepo, manifest.PrintDefinitions, context, now);
            await ReplaceManyAsync(permissionGroupRepo, manifest.FormDataPermissionGroups, context, now);

            await DeleteAsync(formRepo, plan.Forms.StaleIds);
            await DeleteAsync(dashboardRepo, plan.Dashboards.StaleIds);
            await DeleteAsync(dashboardItemRepo, plan.DashboardItems.StaleIds);
            await DeleteAsync(workflowRepo, plan.Workflows.StaleIds);
            await DeleteAsync(printRepo, plan.PrintDefinitions.StaleIds);
            await DeleteAsync(permissionGroupRepo, plan.FormDataPermissionGroups.StaleIds);

            scope.CommitTransaction();

            return new AppPackageImportResult
            {
                AppProfileId = manifest.Profile.Id,
                TemplateId = manifest.Template.Id,
                ProfileCreated = !plan.ProfileExists,
            };
        }

        private AppPackageManifest BuildManifest(AppProfile profile)
        {
            var appTemplateRepo = _resolver.GetRepository<AppTemplate>();
            var formRepo = _resolver.GetRepository<FormTemplate>();
            var dashboardRepo = _resolver.GetRepository<DashboardTemplate>();
            var dashboardItemRepo = _resolver.GetRepository<DashboardItemTemplate>();
            var workflowRepo = _resolver.GetRepository<WfDefinitionTemplate>();
            var printRepo = _resolver.GetRepository<PrintDefTemplate>();
            var permissionGroupRepo = _resolver.GetRepository<FormDataPermissionGroupTemplate>();

            if (string.IsNullOrWhiteSpace(profile.TemplateId))
            {
                throw new BadRequestException("应用市场档案未关联应用模板");
            }

            var template = appTemplateRepo.Get(profile.TemplateId);
            if (template == null || template.DeleteFlag)
            {
                throw new NotFoundException("应用模板不存在");
            }

            var templateId = template.Id;
            return new AppPackageManifest
            {
                Profile = CloneForPackage(profile),
                Template = CloneForPackage(template),
                Forms = formRepo.Queryable.Where(x => x.AppTemplateId == templateId && !x.DeleteFlag).ToList().Select(CloneForPackage).ToList(),
                Dashboards = dashboardRepo.Queryable.Where(x => x.AppTemplateId == templateId && !x.DeleteFlag).ToList().Select(CloneForPackage).ToList(),
                DashboardItems = dashboardItemRepo.Queryable.Where(x => x.AppTemplateId == templateId && !x.DeleteFlag).ToList().Select(CloneForPackage).ToList(),
                Workflows = workflowRepo.Queryable.Where(x => x.AppTemplateId == templateId && !x.DeleteFlag).ToList().Select(CloneForPackage).ToList(),
                PrintDefinitions = printRepo.Queryable.Where(x => x.AppTemplateId == templateId && !x.DeleteFlag).ToList().Select(CloneForPackage).ToList(),
                FormDataPermissionGroups = permissionGroupRepo.Queryable.Where(x => x.AppTemplateId == templateId && !x.DeleteFlag).ToList().Select(CloneForPackage).ToList(),
            };
        }

        private static AppPackageManifest ReadManifest(Stream stream, long fileLength)
        {
            if (stream == null || !stream.CanRead || fileLength <= 0 || fileLength > MaxPackageBytes)
            {
                throw new BadRequestException("应用模板包无效或超过 10 MB 限制");
            }

            try
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                if (archive.Entries.Count != 1)
                {
                    throw new BadRequestException("应用模板包只能包含 manifest.json");
                }

                var entry = archive.GetEntry(ManifestName);
                if (entry == null || entry.Length <= 0 || entry.Length > MaxPackageBytes)
                {
                    throw new BadRequestException("应用模板包缺少有效的 manifest.json");
                }

                using var entryStream = entry.Open();
                var manifest = JsonSerializer.Deserialize<AppPackageManifest>(entryStream, PackageJsonOptions);
                return manifest ?? throw new BadRequestException("应用模板包内容为空");
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                throw new BadRequestException("应用模板包格式无效");
            }
        }

        private static void ValidateManifest(AppPackageManifest manifest)
        {
            if (manifest.FormatVersion != AppPackageManifest.CurrentFormatVersion)
            {
                throw new BadRequestException($"不支持的应用模板包版本: {manifest.FormatVersion}");
            }

            EnsureId(manifest.Profile, "AppProfile");
            EnsureId(manifest.Template, "AppTemplate");
            if (!string.Equals(manifest.Profile.TemplateId, manifest.Template.Id, StringComparison.Ordinal))
            {
                throw new BadRequestException("AppProfile.TemplateId 与 AppTemplate.Id 不一致");
            }

            var templateId = manifest.Template.Id;
            EnsureResources(manifest.Forms, templateId, "FormTemplate", x => x.AppTemplateId);
            EnsureResources(manifest.Dashboards, templateId, "DashboardTemplate", x => x.AppTemplateId);
            EnsureResources(manifest.DashboardItems, templateId, "DashboardItemTemplate", x => x.AppTemplateId);
            EnsureResources(manifest.Workflows, templateId, "WfDefinitionTemplate", x => x.AppTemplateId);
            EnsureResources(manifest.PrintDefinitions, templateId, "PrintDefTemplate", x => x.AppTemplateId);
            EnsureResources(manifest.FormDataPermissionGroups, templateId, "FormDataPermissionGroupTemplate", x => x.AppTemplateId);

            var formIds = manifest.Forms.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var dashboardIds = manifest.Dashboards.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var workflowIds = manifest.Workflows.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var printIds = manifest.PrintDefinitions.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var dashboardLayoutIds = new HashSet<string>(StringComparer.Ordinal);

            ValidateMenus(manifest.Template.Menus, formIds, dashboardIds);
            foreach (var dashboard in manifest.Dashboards)
            {
                CollectDashboardLayoutIds(dashboard.Layout, dashboardLayoutIds);
            }

            foreach (var item in manifest.DashboardItems)
            {
                if (!dashboardIds.Contains(item.DashboardTemplateId))
                {
                    throw new BadRequestException($"仪表盘项 {item.Id} 引用了不存在的仪表盘模板");
                }
                if (!string.IsNullOrWhiteSpace(item.LayoutId) && !dashboardLayoutIds.Contains(item.LayoutId))
                {
                    throw new BadRequestException($"仪表盘项 {item.Id} 引用了不存在的布局块");
                }
                ValidateJsonReferences(item.Details, formIds, dashboardIds, workflowIds, printIds);
            }

            foreach (var workflow in manifest.Workflows)
            {
                if (!string.IsNullOrWhiteSpace(workflow.ExternalTemplateId) && !formIds.Contains(workflow.ExternalTemplateId))
                {
                    throw new BadRequestException($"工作流 {workflow.Id} 引用了不存在的表单模板");
                }
                if (!string.IsNullOrWhiteSpace(workflow.SourceTemplateId)
                    && !formIds.Contains(workflow.SourceTemplateId)
                    && !dashboardIds.Contains(workflow.SourceTemplateId)
                    && !workflowIds.Contains(workflow.SourceTemplateId))
                {
                    throw new BadRequestException($"工作流 {workflow.Id} 引用了不存在的事件来源模板");
                }
                ValidateJsonReferences(workflow.Content, formIds, dashboardIds, workflowIds, printIds);
                ValidateJsonReferences(JsonSerializer.Serialize(workflow.Metadata, PackageJsonOptions), formIds, dashboardIds, workflowIds, printIds);
                if (workflow.EventSetting != null)
                {
                    ValidateJsonReferences(JsonSerializer.Serialize(workflow.EventSetting, PackageJsonOptions), formIds, dashboardIds, workflowIds, printIds);
                }
            }

            foreach (var print in manifest.PrintDefinitions)
            {
                if (!formIds.Contains(print.FormTemplateId))
                {
                    throw new BadRequestException($"打印模板 {print.Id} 引用了不存在的表单模板");
                }
                ValidateJsonReferences(print.Content, formIds, dashboardIds, workflowIds, printIds);
            }

            foreach (var permissionGroup in manifest.FormDataPermissionGroups)
            {
                if (!string.IsNullOrWhiteSpace(permissionGroup.FormTemplateId) && !formIds.Contains(permissionGroup.FormTemplateId))
                {
                    throw new BadRequestException($"授权组 {permissionGroup.Id} 引用了不存在的表单模板");
                }
            }

            foreach (var form in manifest.Forms)
            {
                ValidateJsonReferences(JsonSerializer.Serialize(form.Content, PackageJsonOptions), formIds, dashboardIds, workflowIds, printIds);
                ValidateJsonReferences(JsonSerializer.Serialize(form.FormSettings, PackageJsonOptions), formIds, dashboardIds, workflowIds, printIds);
            }
        }

        private ImportPlan BuildImportPlan(AppPackageManifest manifest)
        {
            var profileRepo = _resolver.GetRepository<AppProfile>();
            var appTemplateRepo = _resolver.GetRepository<AppTemplate>();
            var formRepo = _resolver.GetRepository<FormTemplate>();
            var dashboardRepo = _resolver.GetRepository<DashboardTemplate>();
            var dashboardItemRepo = _resolver.GetRepository<DashboardItemTemplate>();
            var workflowRepo = _resolver.GetRepository<WfDefinitionTemplate>();
            var printRepo = _resolver.GetRepository<PrintDefTemplate>();
            var permissionGroupRepo = _resolver.GetRepository<FormDataPermissionGroupTemplate>();

            var profile = profileRepo.Get(manifest.Profile.Id);
            if (profile != null && (profile.DeleteFlag || !string.Equals(profile.TemplateId, manifest.Template.Id, StringComparison.Ordinal)))
            {
                throw new BadRequestException("目标 AppProfile 已删除或关联了不同的 TemplateId");
            }

            var existingTemplate = appTemplateRepo.Get(manifest.Template.Id);
            if (existingTemplate?.DeleteFlag == true)
            {
                throw new BadRequestException("目标 AppTemplate 已删除，不能直接覆盖");
            }

            VerifyTargetOwnership(formRepo, manifest.Forms, manifest.Template.Id, x => x.AppTemplateId, "FormTemplate");
            VerifyTargetOwnership(dashboardRepo, manifest.Dashboards, manifest.Template.Id, x => x.AppTemplateId, "DashboardTemplate");
            VerifyTargetOwnership(dashboardItemRepo, manifest.DashboardItems, manifest.Template.Id, x => x.AppTemplateId, "DashboardItemTemplate");
            VerifyTargetOwnership(workflowRepo, manifest.Workflows, manifest.Template.Id, x => x.AppTemplateId, "WfDefinitionTemplate");
            VerifyTargetOwnership(printRepo, manifest.PrintDefinitions, manifest.Template.Id, x => x.AppTemplateId, "PrintDefTemplate");
            VerifyTargetOwnership(permissionGroupRepo, manifest.FormDataPermissionGroups, manifest.Template.Id, x => x.AppTemplateId, "FormDataPermissionGroupTemplate");

            return new ImportPlan
            {
                Manifest = manifest,
                ProfileExists = profile != null,
                TemplateExists = existingTemplate != null,
                Forms = BuildState(formRepo.Queryable.Where(x => x.AppTemplateId == manifest.Template.Id), manifest.Forms),
                Dashboards = BuildState(dashboardRepo.Queryable.Where(x => x.AppTemplateId == manifest.Template.Id), manifest.Dashboards),
                DashboardItems = BuildState(dashboardItemRepo.Queryable.Where(x => x.AppTemplateId == manifest.Template.Id), manifest.DashboardItems),
                Workflows = BuildState(workflowRepo.Queryable.Where(x => x.AppTemplateId == manifest.Template.Id), manifest.Workflows),
                PrintDefinitions = BuildState(printRepo.Queryable.Where(x => x.AppTemplateId == manifest.Template.Id), manifest.PrintDefinitions),
                FormDataPermissionGroups = BuildState(permissionGroupRepo.Queryable.Where(x => x.AppTemplateId == manifest.Template.Id), manifest.FormDataPermissionGroups),
            };
        }

        private static AppPackagePreview ToPreview(ImportPlan plan)
        {
            return new AppPackagePreview
            {
                AppProfileId = plan.Manifest.Profile.Id,
                TemplateId = plan.Manifest.Template.Id,
                ProfileExists = plan.ProfileExists,
                ProfileAction = plan.ProfileExists ? "Keep" : "Create",
                Resources =
                [
                    ToResourcePreview("AppTemplate", plan.TemplateExists ? 0 : 1, plan.TemplateExists ? 1 : 0, 0),
                    ToResourcePreview("FormTemplate", plan.Forms),
                    ToResourcePreview("DashboardTemplate", plan.Dashboards),
                    ToResourcePreview("DashboardItemTemplate", plan.DashboardItems),
                    ToResourcePreview("WfDefinitionTemplate", plan.Workflows),
                    ToResourcePreview("PrintDefTemplate", plan.PrintDefinitions),
                    ToResourcePreview("FormDataPermissionGroupTemplate", plan.FormDataPermissionGroups),
                ],
            };
        }

        private static AppPackageResourcePreview ToResourcePreview(string resource, ResourceState state)
        {
            return ToResourcePreview(resource, state.CreateCount, state.UpdateCount, state.StaleIds.Count);
        }

        private static AppPackageResourcePreview ToResourcePreview(string resource, int createCount, int updateCount, int deleteCount)
        {
            return new AppPackageResourcePreview { Resource = resource, CreateCount = createCount, UpdateCount = updateCount, DeleteCount = deleteCount };
        }

        private static ResourceState BuildState<T>(IQueryable<T> existing, IEnumerable<T> incoming) where T : EntityBase
        {
            var incomingIds = incoming.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var existingIds = existing.Where(x => !x.DeleteFlag).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            return new ResourceState
            {
                CreateCount = incomingIds.Count(x => !existingIds.Contains(x)),
                UpdateCount = incomingIds.Count(x => existingIds.Contains(x)),
                StaleIds = existingIds.Where(x => !incomingIds.Contains(x)).ToList(),
            };
        }

        private static async Task ReplaceManyAsync<T>(IRepository<T> repo, IEnumerable<T> entities, IServiceContext context, long now)
            where T : EntityBase
        {
            foreach (var entity in entities)
            {
                await ReplaceAsync(repo, entity, context, now);
            }
        }

        private static async Task ReplaceAsync<T>(IRepository<T> repo, T entity, IServiceContext context, long now)
            where T : EntityBase
        {
            var existing = repo.Get(entity.Id);
            StampForWrite(entity, existing, context, now);
            if (existing == null)
            {
                await repo.InsertAsync(entity);
            }
            else
            {
                await repo.ReplaceAsync(entity);
            }
        }

        private static async Task DeleteAsync<T>(IRepository<T> repo, IReadOnlyCollection<string> ids) where T : class, IMongoEntity
        {
            if (ids.Count > 0)
            {
                await repo.DeleteAsync(ids);
            }
        }

        private static void StampForWrite(EntityBase entity, EntityBase? existing, IServiceContext context, long now)
        {
            entity.DeleteFlag = false;
            entity.CreateBy = existing?.CreateBy ?? context.Operator;
            entity.CreateTime = existing?.CreateTime ?? now;
            entity.UpdateBy = context.Operator;
            entity.UpdateTime = now;
        }

        private static T CloneForPackage<T>(T entity) where T : EntityBase
        {
            var json = JsonSerializer.Serialize(entity, PackageJsonOptions);
            var clone = JsonSerializer.Deserialize<T>(json, PackageJsonOptions) ?? throw new InvalidOperationException("无法序列化模板资源");
            clone.CreateBy = null;
            clone.CreateTime = 0;
            clone.UpdateBy = null;
            clone.UpdateTime = null;
            clone.DeleteFlag = false;
            return clone;
        }

        private static void EnsureId(MongoEntityBase entity, string resourceName)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                throw new BadRequestException($"{resourceName} 缺少 ID");
            }
        }

        private static void EnsureResources<T>(IEnumerable<T> resources, string templateId, string resourceName, Func<T, string> appTemplateId)
            where T : MongoEntityBase
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resource in resources)
            {
                EnsureId(resource, resourceName);
                if (!ids.Add(resource.Id))
                {
                    throw new BadRequestException($"{resourceName} 存在重复 ID: {resource.Id}");
                }
                if (!string.Equals(appTemplateId(resource), templateId, StringComparison.Ordinal))
                {
                    throw new BadRequestException($"{resourceName} {resource.Id} 归属的 AppTemplateId 不正确");
                }
            }
        }

        private static void VerifyTargetOwnership<T>(IRepository<T> repo, IEnumerable<T> incoming, string templateId, Func<T, string> appTemplateId, string resourceName)
            where T : EntityBase
        {
            foreach (var entity in incoming)
            {
                var existing = repo.Get(entity.Id);
                if (existing != null && !string.Equals(appTemplateId(existing), templateId, StringComparison.Ordinal))
                {
                    throw new BadRequestException($"目标 {resourceName} ID 已被其他应用模板占用: {entity.Id}");
                }
            }
        }

        private static void ValidateMenus(string menus, HashSet<string> formIds, HashSet<string> dashboardIds)
        {
            try
            {
                var root = JsonNode.Parse(string.IsNullOrWhiteSpace(menus) ? "[]" : menus) as JsonArray
                    ?? throw new BadRequestException("模板菜单必须是 JSON 数组");
                ValidateMenuNodes(root, formIds, dashboardIds);
            }
            catch (JsonException)
            {
                throw new BadRequestException("模板菜单 JSON 无效");
            }
        }

        private static void ValidateMenuNodes(JsonArray nodes, HashSet<string> formIds, HashSet<string> dashboardIds)
        {
            foreach (var node in nodes)
            {
                if (node is not JsonObject menu)
                {
                    throw new BadRequestException("模板菜单项无效");
                }
                var menuId = menu["menuId"]?.GetValue<string>();
                var menuType = menu["menuType"]?.GetValue<int>();
                if (string.IsNullOrWhiteSpace(menuId) || menuType == null)
                {
                    throw new BadRequestException("模板菜单缺少 menuId 或 menuType");
                }
                if (menuType == (int)FormType.Form && !formIds.Contains(menuId))
                {
                    throw new BadRequestException($"模板菜单引用了不存在的表单模板: {menuId}");
                }
                if (menuType == (int)FormType.Dashboard && !dashboardIds.Contains(menuId))
                {
                    throw new BadRequestException($"模板菜单引用了不存在的仪表盘模板: {menuId}");
                }
                if (menu["subMenus"] is JsonArray subMenus)
                {
                    ValidateMenuNodes(subMenus, formIds, dashboardIds);
                }
            }
        }

        private static void CollectDashboardLayoutIds(string layout, HashSet<string> layoutIds)
        {
            if (string.IsNullOrWhiteSpace(layout))
            {
                return;
            }
            try
            {
                CollectLayoutNode(JsonNode.Parse(layout), layoutIds);
            }
            catch (JsonException)
            {
                throw new BadRequestException("仪表盘布局 JSON 无效");
            }
        }

        private static void CollectLayoutNode(JsonNode? node, HashSet<string> layoutIds)
        {
            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array) CollectLayoutNode(item, layoutIds);
                    break;
                case JsonObject obj:
                    if (obj["i"] is JsonValue value && value.TryGetValue<string>(out var layoutId) && !string.IsNullOrWhiteSpace(layoutId))
                    {
                        layoutIds.Add(layoutId);
                    }
                    foreach (var property in obj) CollectLayoutNode(property.Value, layoutIds);
                    break;
            }
        }

        private static void ValidateJsonReferences(string json, HashSet<string> formIds, HashSet<string> dashboardIds, HashSet<string> workflowIds, HashSet<string> printIds)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }
            try
            {
                ValidateReferenceNode(JsonNode.Parse(json), formIds, dashboardIds, workflowIds, printIds);
            }
            catch (JsonException)
            {
                throw new BadRequestException("模板配置 JSON 无效");
            }
        }

        private static void ValidateReferenceNode(JsonNode? node, HashSet<string> formIds, HashSet<string> dashboardIds, HashSet<string> workflowIds, HashSet<string> printIds)
        {
            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array) ValidateReferenceNode(item, formIds, dashboardIds, workflowIds, printIds);
                    break;
                case JsonObject obj:
                    foreach (var property in obj)
                    {
                        if (property.Value is JsonValue value && value.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id))
                        {
                            var isValid = property.Key.ToLowerInvariant() switch
                            {
                                "formid" or "sourceformid" or "externalid" => formIds.Contains(id),
                                "dashboardid" or "dashid" => dashboardIds.Contains(id),
                                "workflowid" or "eventflowid" => workflowIds.Contains(id),
                                "printid" => printIds.Contains(id),
                                "sourceid" => formIds.Contains(id) || dashboardIds.Contains(id) || workflowIds.Contains(id),
                                _ => true,
                            };
                            if (!isValid)
                            {
                                throw new BadRequestException($"模板配置引用了不存在的资源: {property.Key}={id}");
                            }
                        }
                        ValidateReferenceNode(property.Value, formIds, dashboardIds, workflowIds, printIds);
                    }
                    break;
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = string.Concat((name ?? string.Empty).Select(c => invalidChars.Contains(c) ? '_' : c)).Trim();
            return string.IsNullOrWhiteSpace(safeName) ? "app-package" : safeName;
        }

        private sealed class ImportPlan
        {
            public AppPackageManifest Manifest { get; init; } = new();
            public bool ProfileExists { get; init; }
            public bool TemplateExists { get; init; }
            public ResourceState Forms { get; init; } = new();
            public ResourceState Dashboards { get; init; } = new();
            public ResourceState DashboardItems { get; init; } = new();
            public ResourceState Workflows { get; init; } = new();
            public ResourceState PrintDefinitions { get; init; } = new();
            public ResourceState FormDataPermissionGroups { get; init; } = new();
        }

        private sealed class ResourceState
        {
            public int CreateCount { get; init; }
            public int UpdateCount { get; init; }
            public List<string> StaleIds { get; init; } = [];
        }
    }
}
