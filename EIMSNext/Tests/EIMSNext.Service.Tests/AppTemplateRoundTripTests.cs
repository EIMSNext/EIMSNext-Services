using System.Composition.Hosting;
using System.Text.Json.Nodes;

using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.MongoDb;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.MongoDb;
using EIMSNext.Service;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

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
            var authGroupRepo = repos.Add(new InMemoryRepository<AuthGroup>());
            var authGroupTemplateRepo = repos.Add(new InMemoryRepository<AuthGroupTemplate>());

            const string sourceAppId = "app-source";
            const string sourceFormId = "form-source";
            const string sourceDashboardId = "dashboard-source";
            const string sourceDashboardItemId = "dashboard-item-source";
            const string sourceWorkflowId = "workflow-source";
            const string sourcePrintId = "print-source";
            const string sourceLayoutId = "layout-source";

            await appRepo.InsertAsync(new AppDef
            {
                Id = sourceAppId,
                Name = "Source App",
                Description = "Round-trip test app",
                Icon = "ep:grid",
                IconColor = "#3366ff",
                AppMenus =
                [
                    new AppMenu { MenuId = sourceFormId, Title = "Form", MenuType = FormType.Form },
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
                Layout = $"[{{\"i\":\"{sourceLayoutId}\",\"x\":0,\"y\":0,\"w\":4,\"h\":3}}]"
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
            Assert.AreEqual("Published", profile.Status);
            Assert.AreEqual(sourceForm.TemplateId, workflowTemplate.ExternalTemplateId);
            Assert.AreEqual(sourceForm.TemplateId, workflowTemplate.SourceTemplateId);
            Assert.AreEqual(sourceForm.TemplateId, printTemplate.FormTemplateId);

            var templateMenuIds = JsonNode.Parse(appTemplate.Menus)!
                .AsArray()
                .Select(node => node!["menuId"]!.GetValue<string>())
                .ToList();
            CollectionAssert.Contains(templateMenuIds, sourceForm.TemplateId!);
            CollectionAssert.Contains(templateMenuIds, sourceDashboard.TemplateId!);

            var templateLayoutId = JsonNode.Parse(dashboardTemplate.Layout)![0]!["i"]!.GetValue<string>();
            Assert.AreNotEqual(sourceLayoutId, templateLayoutId);
            Assert.AreEqual(templateLayoutId, dashboardItemTemplate.LayoutId);
            StringAssert.Contains(dashboardItemTemplate.Details, sourceForm.TemplateId!);
            StringAssert.Contains(dashboardItemTemplate.Details, sourceDashboard.TemplateId!);
            StringAssert.Contains(dashboardItemTemplate.Details, sourceWorkflow.TemplateId!);
            StringAssert.Contains(dashboardItemTemplate.Details, sourcePrint.TemplateId!);

            var installedAppId = await installService.InstallAsync(profile.Id);

            var installedApp = appRepo.Get(installedAppId)!;
            var installedForm = formRepo.Queryable.Single(x => x.AppId == installedAppId);
            var installedDashboard = dashboardRepo.Queryable.Single(x => x.AppId == installedAppId);
            var installedDashboardItem = dashboardItemRepo.Queryable.Single(x => x.AppId == installedAppId);
            var installedWorkflow = workflowRepo.Queryable.Single(x => x.AppId == installedAppId);
            var installedPrint = printRepo.Queryable.Single(x => x.AppId == installedAppId);

            Assert.AreNotEqual(sourceAppId, installedAppId);
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
            Assert.AreNotEqual(sourceLayoutId, installedLayoutId);
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
            CollectionAssert.DoesNotContain(installedMenuIds, sourceForm.TemplateId!);
            CollectionAssert.DoesNotContain(installedMenuIds, sourceDashboard.TemplateId!);
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

            public MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null) => throw new NotSupportedException();
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
            public Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
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
            public Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
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
        }
    }
}
