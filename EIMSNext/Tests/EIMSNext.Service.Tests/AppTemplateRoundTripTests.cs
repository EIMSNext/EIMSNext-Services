using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Cache;
using EIMSNext.Service;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class AppTemplateRoundTripTests
    {
        [TestMethod]
        public async Task PublishThenInstall_RewritesTemplateReferencesBackToInstalledIds()
        {
            var repos = new RepositoryRegistry();

            var appRepo = repos.Add(new InMemoryRepository<AppDef>());
            var formRepo = repos.Add(new InMemoryRepository<FormDef>());
            var dashboardRepo = repos.Add(new InMemoryRepository<DashboardDef>());
            var dashboardItemRepo = repos.Add(new InMemoryRepository<DashboardItemDef>());
            var workflowRepo = repos.Add(new InMemoryRepository<Wf_Definition>());
            var printRepo = repos.Add(new InMemoryRepository<PrintDef>());
            var appTemplateRepo = repos.Add(new InMemoryRepository<AppTemplate>());
            var formTemplateRepo = repos.Add(new InMemoryRepository<FormTemplate>());
            var dashboardTemplateRepo = repos.Add(new InMemoryRepository<DashboardTemplate>());
            var dashboardItemTemplateRepo = repos.Add(new InMemoryRepository<DashboardItemTemplate>());
            var workflowTemplateRepo = repos.Add(new InMemoryRepository<WfDefinitionTemplate>());
            var printTemplateTemplateRepo = repos.Add(new InMemoryRepository<PrintDefTemplate>());
            var profileRepo = repos.Add(new InMemoryRepository<AppProfile>());
            var permissionGroupRepo = repos.Add(new InMemoryRepository<FormDataPermissionGroup>());
            var permissionGroupTemplateRepo = repos.Add(new InMemoryRepository<FormDataPermissionGroupTemplate>());
            repos.AddService<IServiceContext>(new TestServiceContext
            {
                CorpId = "corp-installed",
                Operator = new Operator("employee-1", "E001", "Installer")
            });

            const string sourceAppId = "app-source";
            const string sourceFormId = "form-source";
            const string sourceDashboardId = "dashboard-source";
            const string sourceDashboardItemId = "dashboard-item-source";
            const string sourceWorkflowId = "workflow-source";
            const string sourcePrintId = "print-source";
            const string sourceLayoutId = "layout-source";
            const string sourceChildLayoutId = "layout-child-source";

            await appRepo.InsertAsync(new AppDef
            {
                Id = sourceAppId,
                Name = "Source App",
                Description = "Round-trip test app",
                Icon = "ep:grid",
                IconColor = "#3366ff",
                AppMenus =
                [
                    new AppMenu
                    {
                        MenuId = sourceFormId,
                        Title = "Form",
                        MenuType = FormType.Form,
                        Editable = false,
                        Deletable = false,
                        ListComponent = "custom/orders/index"
                    },
                    new AppMenu { MenuId = sourceDashboardId, Title = "Dashboard", MenuType = FormType.Dashboard }
                ]
            });

            await formRepo.InsertAsync(new FormDef
            {
                Id = sourceFormId,
                AppId = sourceAppId,
                Name = "Source Form",
                UsingWorkflow = true,
                FormSettings = new FormSettings
                {
                    Advanced = new DataAdvancedSettings
                    {
                        DataTitle = new DataTitleSettings { Mode = "custom", Content = "Test Title" }
                    }
                }
            });

            await dashboardRepo.InsertAsync(new DashboardDef
            {
                Id = sourceDashboardId,
                AppId = sourceAppId,
                Name = "Source Dashboard",
                Layout = $"[{{\"i\":\"{sourceLayoutId}\",\"x\":0,\"y\":0,\"w\":4,\"h\":3}},{{\"i\":\"{sourceChildLayoutId}\",\"parentLayoutId\":\"{sourceLayoutId}\",\"x\":0,\"y\":0,\"w\":4,\"h\":3}}]"
            });

            await workflowRepo.InsertAsync(new Wf_Definition
            {
                Id = sourceWorkflowId,
                AppId = sourceAppId,
                Name = "Source Workflow",
                ExternalId = sourceFormId,
                Content = $"{{\"formId\":\"{sourceFormId}\",\"dashboardId\":\"{sourceDashboardId}\",\"workflowId\":\"{sourceWorkflowId}\",\"printId\":\"{sourcePrintId}\",\"appId\":\"{sourceAppId}\"}}",
                Metadata = new WfMetadata(),
                SourceId = sourceFormId,
                IsCurrent = true
            });

            await printRepo.InsertAsync(new PrintDef
            {
                Id = sourcePrintId,
                AppId = sourceAppId,
                FormId = sourceFormId,
                Name = "Source Print",
                Content = $"{{\"formId\":\"{sourceFormId}\",\"workflowId\":\"{sourceWorkflowId}\",\"printId\":\"{sourcePrintId}\",\"appId\":\"{sourceAppId}\"}}",
                PrintType = PrintDefType.Pdf
            });

            await dashboardItemRepo.InsertAsync(new DashboardItemDef
            {
                Id = sourceDashboardItemId,
                AppId = sourceAppId,
                DashboardId = sourceDashboardId,
                LayoutId = sourceLayoutId,
                ItemType = "chart",
                Name = "Source Item",
                Details = $"{{\"formId\":\"{sourceFormId}\",\"dashboardId\":\"{sourceDashboardId}\",\"workflowId\":\"{sourceWorkflowId}\",\"printId\":\"{sourcePrintId}\",\"appId\":\"{sourceAppId}\"}}"
            });

            var resolver = new TestResolver(repos.Services);
            var publishService = new AppPublishService(resolver);
            var installService = new AppInstallService(resolver);

            var appTemplateId = await publishService.PublishAsync(sourceAppId);

            var sourceApp = appRepo.Get(sourceAppId)!;
            var sourceForm = formRepo.Get(sourceFormId)!;
            var sourceDashboard = dashboardRepo.Get(sourceDashboardId)!;
            var sourceDashboardItem = dashboardItemRepo.Get(sourceDashboardItemId)!;
            var sourceWorkflow = workflowRepo.Get(sourceWorkflowId)!;
            var sourcePrint = printRepo.Get(sourcePrintId)!;

            Assert.AreEqual(appTemplateId, sourceApp.TemplateId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(sourceForm.TemplateId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sourceDashboard.TemplateId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sourceDashboardItem.TemplateId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sourceWorkflow.TemplateId));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sourcePrint.TemplateId));

            var appTemplate = appTemplateRepo.Get(appTemplateId)!;
            var dashboardTemplate = dashboardTemplateRepo.Get(sourceDashboard.TemplateId!)!;
            var dashboardItemTemplate = dashboardItemTemplateRepo.Get(sourceDashboardItem.TemplateId!)!;
            var workflowTemplate = workflowTemplateRepo.Get(sourceWorkflow.TemplateId!)!;
            var printTemplate = printTemplateTemplateRepo.Get(sourcePrint.TemplateId!)!;
            var profile = profileRepo.Queryable.Single();

            Assert.AreEqual(appTemplateId, profile.TemplateId);
            Assert.AreEqual(AppProfileStatus.Published, profile.Status);
            Assert.AreEqual(sourceForm.TemplateId, workflowTemplate.ExternalTemplateId);
            Assert.AreEqual(sourceForm.TemplateId, workflowTemplate.SourceTemplateId);
            Assert.AreEqual(sourceForm.TemplateId, printTemplate.FormTemplateId);

            var templateMenuIds = JsonNode.Parse(appTemplate.Menus)!
                .AsArray()
                .Select(node => node!["menuId"]!.GetValue<string>())
                .ToList();
            CollectionAssert.Contains(templateMenuIds, sourceForm.TemplateId!);
            CollectionAssert.Contains(templateMenuIds, sourceDashboard.TemplateId!);
            var templateFormMenu = JsonNode.Parse(appTemplate.Menus)!.AsArray()
                .Single(node => node!["menuId"]!.GetValue<string>() == sourceForm.TemplateId);
            Assert.IsFalse(templateFormMenu!["editable"]!.GetValue<bool>());
            Assert.IsFalse(templateFormMenu["deletable"]!.GetValue<bool>());
            Assert.AreEqual("custom/orders/index", templateFormMenu["listComponent"]!.GetValue<string>());

            var templateLayoutId = JsonNode.Parse(dashboardTemplate.Layout)![0]!["i"]!.GetValue<string>();
            var templateChildParentLayoutId = JsonNode.Parse(dashboardTemplate.Layout)![1]!["parentLayoutId"]!.GetValue<string>();
            Assert.AreNotEqual(sourceLayoutId, templateLayoutId);
            Assert.AreEqual(templateLayoutId, templateChildParentLayoutId);
            Assert.AreEqual(templateLayoutId, dashboardItemTemplate.LayoutId);
            StringAssert.Contains(dashboardItemTemplate.Details, sourceForm.TemplateId!);
            StringAssert.Contains(dashboardItemTemplate.Details, sourceDashboard.TemplateId!);
            StringAssert.Contains(dashboardItemTemplate.Details, sourceWorkflow.TemplateId!);
            StringAssert.Contains(dashboardItemTemplate.Details, sourcePrint.TemplateId!);

            var originalProfileId = profile.Id;
            var originalPublishedAt = profile.PublishedAt;
            var originalFormTemplateId = sourceForm.TemplateId;
            const string addedFormId = "form-added";
            await formRepo.InsertAsync(new FormDef
            {
                Id = addedFormId,
                AppId = sourceAppId,
                Name = "Added Form"
            });
            sourceApp.AppMenus.Add(new AppMenu { MenuId = addedFormId, Title = "Added Form", MenuType = FormType.Form });
            await appRepo.ReplaceAsync(sourceApp);

            var republishedTemplateId = await publishService.PublishAsync(sourceAppId);
            var addedForm = formRepo.Get(addedFormId)!;
            Assert.AreEqual(appTemplateId, republishedTemplateId);
            Assert.AreEqual(originalFormTemplateId, formRepo.Get(sourceFormId)!.TemplateId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(addedForm.TemplateId));
            Assert.IsNotNull(formTemplateRepo.Get(addedForm.TemplateId!));
            Assert.AreEqual(originalProfileId, profileRepo.Queryable.Single().Id);
            Assert.AreEqual(originalPublishedAt, profileRepo.Queryable.Single().PublishedAt);
            Assert.AreEqual(0L, profileRepo.Queryable.Single().InstallCount);

            var installedAppId = await installService.InstallAsync(profile.Id);

            var installedApp = appRepo.Get(installedAppId)!;
            var installedForm = formRepo.Queryable.Single(x => x.AppId == installedAppId && x.TemplateId == sourceForm.TemplateId);
            var installedDashboard = dashboardRepo.Queryable.Single(x => x.AppId == installedAppId);
            var installedDashboardItem = dashboardItemRepo.Queryable.Single(x => x.AppId == installedAppId);
            var installedWorkflow = workflowRepo.Queryable.Single(x => x.AppId == installedAppId);
            var installedPrint = printRepo.Queryable.Single(x => x.AppId == installedAppId);

            Assert.AreNotEqual(sourceAppId, installedAppId);
            Assert.AreEqual("corp-installed", installedApp.CorpId);
            Assert.AreEqual("employee-1", installedApp.CreateBy?.Id);
            Assert.AreEqual(appTemplateId, installedApp.TemplateId);
            Assert.AreEqual(sourceForm.TemplateId, installedForm.TemplateId);
            Assert.AreEqual(sourceDashboard.TemplateId, installedDashboard.TemplateId);
            Assert.AreEqual(sourceDashboardItem.TemplateId, installedDashboardItem.TemplateId);
            Assert.AreEqual(sourceWorkflow.TemplateId, installedWorkflow.TemplateId);
            Assert.AreEqual(sourcePrint.TemplateId, installedPrint.TemplateId);
            Assert.AreEqual(1L, profileRepo.Get(profile.Id)!.InstallCount);

            Assert.AreEqual(installedForm.Id, installedWorkflow.ExternalId);
            Assert.AreEqual(installedForm.Id, installedWorkflow.SourceId);
            Assert.AreEqual(installedForm.Id, installedPrint.FormId);

            var installedLayoutId = JsonNode.Parse(installedDashboard.Layout)![0]!["i"]!.GetValue<string>();
            var installedChildParentLayoutId = JsonNode.Parse(installedDashboard.Layout)![1]!["parentLayoutId"]!.GetValue<string>();
            Assert.AreNotEqual(sourceLayoutId, installedLayoutId);
            Assert.AreEqual(installedLayoutId, installedChildParentLayoutId);
            Assert.AreEqual(installedLayoutId, installedDashboardItem.LayoutId);

            StringAssert.Contains(installedDashboardItem.Details, installedForm.Id);
            StringAssert.Contains(installedDashboardItem.Details, installedDashboard.Id);
            StringAssert.Contains(installedDashboardItem.Details, installedWorkflow.Id);
            StringAssert.Contains(installedDashboardItem.Details, installedPrint.Id);
            StringAssert.Contains(installedDashboardItem.Details, installedAppId);
            StringAssert.Contains(installedWorkflow.Content, installedForm.Id);
            StringAssert.Contains(installedWorkflow.Content, installedDashboard.Id);
            StringAssert.Contains(installedWorkflow.Content, installedPrint.Id);
            StringAssert.Contains(installedWorkflow.Content, installedAppId);
            StringAssert.Contains(installedPrint.Content, installedForm.Id);
            StringAssert.Contains(installedPrint.Content, installedWorkflow.Id);
            StringAssert.Contains(installedPrint.Content, installedPrint.Id);
            StringAssert.Contains(installedPrint.Content, installedAppId);

            var installedMenuIds = installedApp.AppMenus.Select(x => x.MenuId).ToList();
            CollectionAssert.Contains(installedMenuIds, installedForm.Id);
            CollectionAssert.Contains(installedMenuIds, installedDashboard.Id);
            var installedFormMenu = installedApp.AppMenus.Single(x => x.MenuId == installedForm.Id);
            Assert.IsFalse(installedFormMenu.Editable);
            Assert.IsFalse(installedFormMenu.Deletable);
            Assert.AreEqual("custom/orders/index", installedFormMenu.ListComponent);
            CollectionAssert.DoesNotContain(installedMenuIds, sourceForm.TemplateId!);
            CollectionAssert.DoesNotContain(installedMenuIds, sourceDashboard.TemplateId!);

            sourceForm.DeleteFlag = true;
            await formRepo.ReplaceAsync(sourceForm);
            await publishService.PublishAsync(sourceAppId);
            Assert.IsNull(formTemplateRepo.Get(sourceForm.TemplateId!));
            var republishedMenuIds = JsonNode.Parse(appTemplateRepo.Get(appTemplateId)!.Menus)!
                .AsArray()
                .Select(node => node!["menuId"]!.GetValue<string>())
                .ToList();
            CollectionAssert.DoesNotContain(republishedMenuIds, sourceFormId);
            CollectionAssert.DoesNotContain(republishedMenuIds, sourceForm.TemplateId!);

            profile.Status = AppProfileStatus.Draft;
            await profileRepo.ReplaceAsync(profile);
            await Assert.ThrowsExactlyAsync<EIMSNext.Common.NotFoundException>(() => installService.InstallAsync(profile.Id));
        }

        [TestMethod]
        public async Task AppPackage_ImportsByProfileId_PreservesExistingProfileAndReplacesTemplateResources()
        {
            const string profileId = "profile-package";
            const string templateId = "template-package";
            const string formId = "form-package";
            const string dashboardId = "dashboard-package";
            const string layoutId = "layout-package";

            var source = new RepositoryRegistry();
            var sourceProfileRepo = source.Add(new InMemoryRepository<AppProfile>());
            source.Add(new InMemoryRepository<AppTemplate>());
            source.Add(new InMemoryRepository<FormTemplate>());
            source.Add(new InMemoryRepository<DashboardTemplate>());
            source.Add(new InMemoryRepository<DashboardItemTemplate>());
            source.Add(new InMemoryRepository<WfDefinitionTemplate>());
            source.Add(new InMemoryRepository<PrintDefTemplate>());
            source.Add(new InMemoryRepository<FormDataPermissionGroupTemplate>());
            source.AddService<IServiceContext>(new TestServiceContext { Operator = new Operator("source", "S001", "Source") });

            await sourceProfileRepo.InsertAsync(new AppProfile
            {
                Id = profileId,
                TemplateId = templateId,
                Name = "Source Profile",
                Summary = "source summary",
                InstallCount = 4,
                Status = AppProfileStatus.Published,
            });
            await ((InMemoryRepository<AppTemplate>)source.Services[typeof(IRepository<AppTemplate>)]).InsertAsync(new AppTemplate
            {
                Id = templateId,
                Name = "Source Template",
                Menus = $"[{{\"menuId\":\"{formId}\",\"menuType\":0}}]",
            });
            await ((InMemoryRepository<FormTemplate>)source.Services[typeof(IRepository<FormTemplate>)]).InsertAsync(new FormTemplate
            {
                Id = formId,
                AppTemplateId = templateId,
                Name = "Source Form",
            });
            await ((InMemoryRepository<DashboardTemplate>)source.Services[typeof(IRepository<DashboardTemplate>)]).InsertAsync(new DashboardTemplate
            {
                Id = dashboardId,
                AppTemplateId = templateId,
                Name = "Source Dashboard",
                Layout = $"[{{\"i\":\"{layoutId}\"}}]",
            });
            await ((InMemoryRepository<DashboardItemTemplate>)source.Services[typeof(IRepository<DashboardItemTemplate>)]).InsertAsync(new DashboardItemTemplate
            {
                Id = "dashboard-item-package",
                AppTemplateId = templateId,
                DashboardTemplateId = dashboardId,
                LayoutId = layoutId,
                Details = $"{{\"formId\":\"{formId}\"}}",
            });
            await ((InMemoryRepository<WfDefinitionTemplate>)source.Services[typeof(IRepository<WfDefinitionTemplate>)]).InsertAsync(new WfDefinitionTemplate
            {
                Id = "workflow-package",
                AppTemplateId = templateId,
                ExternalTemplateId = formId,
                SourceTemplateId = formId,
                Content = $"{{\"formId\":\"{formId}\"}}",
            });
            await ((InMemoryRepository<PrintDefTemplate>)source.Services[typeof(IRepository<PrintDefTemplate>)]).InsertAsync(new PrintDefTemplate
            {
                Id = "print-package",
                AppTemplateId = templateId,
                FormTemplateId = formId,
                Content = $"{{\"formId\":\"{formId}\"}}",
            });
            await ((InMemoryRepository<FormDataPermissionGroupTemplate>)source.Services[typeof(IRepository<FormDataPermissionGroupTemplate>)]).InsertAsync(new FormDataPermissionGroupTemplate
            {
                Id = "auth-group-package",
                AppTemplateId = templateId,
                FormTemplateId = formId,
            });

            var exported = await new AppPackageService(new TestResolver(source.Services)).ExportAsync(profileId);

            var target = new RepositoryRegistry();
            var targetProfileRepo = target.Add(new InMemoryRepository<AppProfile>());
            target.Add(new InMemoryRepository<AppTemplate>());
            var targetFormRepo = target.Add(new InMemoryRepository<FormTemplate>());
            target.Add(new InMemoryRepository<DashboardTemplate>());
            target.Add(new InMemoryRepository<DashboardItemTemplate>());
            target.Add(new InMemoryRepository<WfDefinitionTemplate>());
            target.Add(new InMemoryRepository<PrintDefTemplate>());
            target.Add(new InMemoryRepository<FormDataPermissionGroupTemplate>());
            target.AddService<IServiceContext>(new TestServiceContext { Operator = new Operator("target", "T001", "Target") });
            await targetProfileRepo.InsertAsync(new AppProfile
            {
                Id = profileId,
                TemplateId = templateId,
                Name = "Production Profile",
                Summary = "production summary",
                InstallCount = 99,
                Status = AppProfileStatus.Offline,
            });
            await targetFormRepo.InsertAsync(new FormTemplate { Id = "form-stale", AppTemplateId = templateId, Name = "Stale" });

            var targetService = new AppPackageService(new TestResolver(target.Services));
            await using (var previewStream = new MemoryStream(exported.Content))
            {
                var preview = await targetService.PreviewAsync(previewStream, exported.Content.Length);
                Assert.IsTrue(preview.ProfileExists);
                Assert.AreEqual("Keep", preview.ProfileAction);
                Assert.AreEqual(1, preview.Resources.Single(x => x.Resource == "FormTemplate").DeleteCount);
            }
            await using (var importStream = new MemoryStream(exported.Content))
            {
                var result = await targetService.ImportAsync(importStream, exported.Content.Length);
                Assert.IsFalse(result.ProfileCreated);
            }

            var targetProfile = targetProfileRepo.Get(profileId)!;
            Assert.AreEqual("Production Profile", targetProfile.Name);
            Assert.AreEqual("production summary", targetProfile.Summary);
            Assert.AreEqual(99L, targetProfile.InstallCount);
            Assert.AreEqual(AppProfileStatus.Offline, targetProfile.Status);
            Assert.IsNotNull(((InMemoryRepository<AppTemplate>)target.Services[typeof(IRepository<AppTemplate>)]).Get(templateId));
            Assert.IsNotNull(targetFormRepo.Get(formId));
            Assert.IsNull(targetFormRepo.Get("form-stale"));
            Assert.IsNotNull(((InMemoryRepository<DashboardTemplate>)target.Services[typeof(IRepository<DashboardTemplate>)]).Get(dashboardId));
            Assert.IsNotNull(((InMemoryRepository<DashboardItemTemplate>)target.Services[typeof(IRepository<DashboardItemTemplate>)]).Get("dashboard-item-package"));
            Assert.IsNotNull(((InMemoryRepository<WfDefinitionTemplate>)target.Services[typeof(IRepository<WfDefinitionTemplate>)]).Get("workflow-package"));
            Assert.IsNotNull(((InMemoryRepository<PrintDefTemplate>)target.Services[typeof(IRepository<PrintDefTemplate>)]).Get("print-package"));
            Assert.IsNotNull(((InMemoryRepository<FormDataPermissionGroupTemplate>)target.Services[typeof(IRepository<FormDataPermissionGroupTemplate>)]).Get("auth-group-package"));

            targetProfile.TemplateId = "different-template";
            await targetProfileRepo.ReplaceAsync(targetProfile);
            await using (var conflictStream = new MemoryStream(exported.Content))
            {
                await Assert.ThrowsExactlyAsync<EIMSNext.Common.BadRequestException>(() => targetService.ImportAsync(conflictStream, exported.Content.Length));
            }
            Assert.IsNotNull(targetFormRepo.Get(formId));

            var freshTarget = new RepositoryRegistry();
            var freshProfileRepo = freshTarget.Add(new InMemoryRepository<AppProfile>());
            freshTarget.Add(new InMemoryRepository<AppTemplate>());
            freshTarget.Add(new InMemoryRepository<FormTemplate>());
            freshTarget.Add(new InMemoryRepository<DashboardTemplate>());
            freshTarget.Add(new InMemoryRepository<DashboardItemTemplate>());
            freshTarget.Add(new InMemoryRepository<WfDefinitionTemplate>());
            freshTarget.Add(new InMemoryRepository<PrintDefTemplate>());
            freshTarget.Add(new InMemoryRepository<FormDataPermissionGroupTemplate>());
            freshTarget.AddService<IServiceContext>(new TestServiceContext { Operator = new Operator("fresh", "F001", "Fresh") });
            await using (var importStream = new MemoryStream(exported.Content))
            {
                var result = await new AppPackageService(new TestResolver(freshTarget.Services)).ImportAsync(importStream, exported.Content.Length);
                Assert.IsTrue(result.ProfileCreated);
            }
            var importedProfile = freshProfileRepo.Get(profileId)!;
            Assert.AreEqual("Source Profile", importedProfile.Name);
            Assert.AreEqual("source summary", importedProfile.Summary);
            Assert.AreEqual(4L, importedProfile.InstallCount);
            Assert.AreEqual(AppProfileStatus.Published, importedProfile.Status);
        }

        private sealed class RepositoryRegistry
        {
            private readonly Dictionary<Type, object> _services = [];

            public IReadOnlyDictionary<Type, object> Services => _services;

            public InMemoryRepository<T> Add<T>(InMemoryRepository<T> repository) where T : class, IMongoEntity
            {
                _services[typeof(IRepository<T>)] = repository;
                return repository;
            }

            public void AddService<T>(T service) where T : class
            {
                _services[typeof(T)] = service;
            }
        }

        private sealed class TestResolver(IReadOnlyDictionary<Type, object> services) : IResolver
        {
            public CompositionContainer MefContainer => throw new NotSupportedException();

            public object Resolve(Type type, string? name = null) => services[type];

            public T Resolve<T>(string? name = null) where T : class => (T)services[typeof(T)];

            public T GetExport<T>(string? name = null) where T : class => Resolve<T>(name);

            public object GetExport(Type type, string? name = null) => Resolve(type, name);

            public IEnumerable<T> GetExports<T>(string? name = null) where T : class => [Resolve<T>(name)];

            public IEnumerable<object> GetExports(Type type, string? name = null) => [Resolve(type, name)];
        }

        private sealed class InMemoryRepository<T> : IRepository<T> where T : class, IMongoEntity
        {
            private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);
            private int _nextId;

            public IMongoDbContex DbContext => throw new NotSupportedException();
            public IMongoCollection<T> Collection => throw new NotSupportedException();
            public IQueryable<T> Queryable => _items.Values.AsQueryable();
            public FilterDefinitionBuilder<T> FilterBuilder => Builders<T>.Filter;
            public SortDefinitionBuilder<T> SortBuilder => Builders<T>.Sort;
            public SearchDefinitionBuilder<T> SearchBuilder => Builders<T>.Search;
            public ProjectionDefinitionBuilder<T> ProjectionBuilder => Builders<T>.Projection;
            public UpdateDefinitionBuilder<T> UpdateBuilder => Builders<T>.Update;

            public MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null)
            {
                // The in-memory repository has no Mongo session. An uninitialized non-root scope is a no-op on commit/dispose.
                return (MongoTransactionScope)RuntimeHelpers.GetUninitializedObject(typeof(MongoTransactionScope));
            }
            public IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(System.Linq.Expressions.Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();

            public T? Get(string id, IClientSessionHandle? session = null) => _items.TryGetValue(id, out var entity) ? entity : null;
            public Task<T?> GetAsync(string id, IClientSessionHandle? session = null) => Task.FromResult(Get(id, session));

            public long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(System.Linq.Expressions.Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Queryable.LongCount(filter);
            public long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(System.Linq.Expressions.Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Task.FromResult(Count(filter, session, options));
            public Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();

            public void Insert(T entity, IClientSessionHandle? session = null) => _items[EnsureId(entity).Id] = entity;

            public void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null)
            {
                foreach (var entity in entities)
                {
                    Insert(entity, session);
                }
            }

            public Task InsertAsync(T entity, IClientSessionHandle? session = null)
            {
                Insert(entity, session);
                return Task.CompletedTask;
            }

            public Task InsertAsync(IEnumerable<T> entities, IClientSessionHandle? session = null)
            {
                Insert(entities, session);
                return Task.CompletedTask;
            }

            public UpdateResult Update(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
            {
                if (_items.TryGetValue(id, out var entity) && entity is AppProfile profile)
                {
                    profile.InstallCount += 1;
                    return Task.FromResult<UpdateResult>(null!);
                }

                throw new NotSupportedException();
            }
            public UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateManyAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();

            public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null)
            {
                _items[EnsureId(entity).Id] = entity;
                return null!;
            }

            public Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null)
            {
                Replace(entity, session);
                return Task.FromResult<ReplaceOneResult>(null!);
            }

            public DeleteResult Delete(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null)
            {
                foreach (var id in ids)
                {
                    _items.Remove(id);
                }

                return Task.FromResult<DeleteResult>(null!);
            }
            public Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();

            public IEnumerable<T> EnsureId(IEnumerable<T> entities)
            {
                foreach (var entity in entities)
                {
                    yield return EnsureId(entity);
                }
            }

            public T EnsureId(T entity)
            {
                if (string.IsNullOrWhiteSpace(entity.Id))
                {
                    entity.Id = NewId();
                }

                return entity;
            }

            public string NewId() => $"{typeof(T).Name}-{++_nextId}";

            public Task<List<BsonValue>> DistinctFieldValuesAsync(DynamicFilter filter, string field, IClientSessionHandle? session = null)
            {
                return Task.FromResult(new List<BsonValue>());
            }
        }

        private sealed class TestServiceContext : IServiceContext
        {
            public string AccessToken { get; set; } = string.Empty;
            public string CorpId { get; set; } = string.Empty;
            public Operator? Operator { get; set; }
            public string UserId { get; set; } = string.Empty;
            public IUser? User { get; set; }
            public IEmployee? Employee { get; set; }
            public string? ClientIp { get; set; }
            public DataAction Action { get; set; }
            public IScopeCache ScopeCache => null!;
        }
    }
}
