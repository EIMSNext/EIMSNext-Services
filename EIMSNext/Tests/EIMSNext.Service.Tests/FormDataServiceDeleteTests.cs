using System.Composition.Hosting;
using System.Linq.Expressions;
using System.Text.Json;

using EIMSNext.ApiClient.Flow;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Entities;

using HKH.Mef2.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using EIMSNext.TestSupport;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class FormDataServiceDeleteTests
    {
        [TestMethod]
        public void DeleteCore_DraftWithoutTaskLog_HardDeletesInsteadOfRecycling()
        {
            var service = TestableFormDataService.Create();
            var data = NewFormData("draft-no-log", FlowStatus.Draft);
            data.Data.TryAdd("title", "Draft Title");

            service.InvokeDeleteCore([data]);

            CollectionAssert.AreEquivalent(new[] { data.Id }, service.HardDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.RelatedDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.WorkflowDeletedDataIds);
            Assert.AreEqual(0, service.LogicDeletedIds.Count);

            var physicalDeleteLog = AssertHasSinglePhysicalDeleteLog(service, data.Id);
            Assert.IsNotNull(physicalDeleteLog.OldData);
            using var oldData = JsonDocument.Parse(physicalDeleteLog.OldData);
            Assert.AreEqual(data.Id, GetString(oldData.RootElement, "id"));
            Assert.AreEqual("app-1", GetString(oldData.RootElement, "appId"));
            Assert.AreEqual("form-1", GetString(oldData.RootElement, "formId"));
            Assert.AreEqual("Draft Title", GetString(GetProperty(oldData.RootElement, "data"), "title"));
        }

        [TestMethod]
        public void DeleteCore_DraftWithTaskLog_HardDeletesInsteadOfRecycling()
        {
            var service = TestableFormDataService.Create();
            var data = NewFormData("draft-with-log", FlowStatus.Draft);

            service.InvokeDeleteCore([data]);

            CollectionAssert.AreEquivalent(new[] { data.Id }, service.HardDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.RelatedDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.WorkflowDeletedDataIds);
            Assert.AreEqual(0, service.LogicDeletedIds.Count);
            AssertHasSinglePhysicalDeleteLog(service, data.Id);
        }

        [TestMethod]
        public void DeleteCore_NonDraftData_RecyclesData()
        {
            var service = TestableFormDataService.Create();
            var data = NewFormData("approved-data", FlowStatus.Approved);

            service.InvokeDeleteCore([data]);

            CollectionAssert.AreEquivalent(new[] { data.Id }, service.LogicDeletedIds);
            Assert.AreEqual(0, service.HardDeletedIds.Count);
            Assert.AreEqual(0, service.RelatedDeletedIds.Count);
            Assert.AreEqual(0, service.WorkflowDeletedDataIds.Count);
        }

        [TestMethod]
        public void DeleteCore_ActiveWorkflowData_IsRejected()
        {
            var service = TestableFormDataService.Create();
            var approving = NewFormData("approving-data", FlowStatus.Approving);
            var suspended = NewFormData("suspended-data", FlowStatus.Suspended);

            var approvingError = Assert.ThrowsExactly<BadRequestException>(() => service.InvokeDeleteCore([approving]));
            var suspendedError = Assert.ThrowsExactly<BadRequestException>(() => service.InvokeDeleteCore([suspended]));

            StringAssert.Contains(approvingError.Message, "不允许删除");
            StringAssert.Contains(suspendedError.Message, "不允许删除");
            Assert.AreEqual(0, service.LogicDeletedIds.Count);
            Assert.AreEqual(0, service.HardDeletedIds.Count);
        }

        [TestMethod]
        public async Task DeleteCoreAsync_ActiveWorkflowData_IsRejected()
        {
            var service = TestableFormDataService.Create();
            var approving = NewFormData("approving-data-async", FlowStatus.Approving);

            var error = await Assert.ThrowsExactlyAsync<BadRequestException>(() => service.InvokeDeleteCoreAsync([approving]));

            StringAssert.Contains(error.Message, "不允许删除");
            Assert.AreEqual(0, service.LogicDeletedIds.Count);
            Assert.AreEqual(0, service.HardDeletedIds.Count);
        }

        [TestMethod]
        public async Task PurgeCore_DeletedTargets_HardDeletesDataAndStrongRelations()
        {
            var service = TestableFormDataService.Create();
            var data = NewFormData("purge-data", FlowStatus.Approved, deleteFlag: true);

            await service.InvokePurgeCoreAsync([data.Id], [data]);

            CollectionAssert.AreEquivalent(new[] { data.Id }, service.HardDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.RelatedDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.WorkflowDeletedDataIds);
            Assert.AreEqual(0, service.LogicDeletedIds.Count);
        }

        [TestMethod]
        public async Task PurgeCore_OnlyDeletesResolvedRecycleBinTargets()
        {
            var service = TestableFormDataService.Create();
            var data = NewFormData("purge-target", FlowStatus.Approved, deleteFlag: true);

            await service.InvokePurgeCoreAsync(["purge-target", "other-data"], [data]);

            CollectionAssert.AreEquivalent(new[] { data.Id }, service.HardDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.RelatedDeletedIds);
            CollectionAssert.AreEquivalent(new[] { data.Id }, service.WorkflowDeletedDataIds);
            Assert.IsFalse(service.HardDeletedIds.Contains("other-data"));
            Assert.IsFalse(service.RelatedDeletedIds.Contains("other-data"));
            Assert.IsFalse(service.WorkflowDeletedDataIds.Contains("other-data"));
        }

        [TestMethod]
        public void EnsureCanEdit_WorkflowActiveOrTerminalData_IsRejected()
        {
            var service = TestableFormDataService.Create();
            foreach (var status in new[] { FlowStatus.Approving, FlowStatus.Approved, FlowStatus.Suspended, FlowStatus.Discarded })
            {
                var error = Assert.ThrowsExactly<BadRequestException>(() => service.InvokeEnsureCanEdit(NewFormData($"workflow-{status}", status), usingWorkflow: true));
                StringAssert.Contains(error.Message, "不允许修改");
            }
        }

        [TestMethod]
        public void EnsureCanEdit_DraftRejectedAndNonWorkflowApprovedData_AreAllowed()
        {
            var service = TestableFormDataService.Create();
            service.InvokeEnsureCanEdit(NewFormData("workflow-draft", FlowStatus.Draft), usingWorkflow: true);
            service.InvokeEnsureCanEdit(NewFormData("workflow-rejected", FlowStatus.Rejected), usingWorkflow: true);
            service.InvokeEnsureCanEdit(NewFormData("plain-approved", FlowStatus.Approved), usingWorkflow: false);
        }

        private static AuditLog AssertHasSinglePhysicalDeleteLog(TestableFormDataService service, string dataId)
        {
            var physicalDeleteLogs = service.AuditLogs
                .Where(x => x.Action == DbAction.PhysicalDelete)
                .ToList();
            Assert.AreEqual(1, physicalDeleteLogs.Count);
            Assert.AreEqual(dataId, physicalDeleteLogs[0].DataId);
            Assert.AreEqual(nameof(FormData), physicalDeleteLogs[0].EntityType);
            return physicalDeleteLogs[0];
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return GetProperty(element, propertyName).GetString();
        }

        private static JsonElement GetProperty(JsonElement element, string propertyName)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }

            Assert.Fail($"Property '{propertyName}' was not found in {element.GetRawText()}.");
            return default;
        }

        private static FormData NewFormData(string id, FlowStatus flowStatus, bool deleteFlag = false)
        {
            return new FormData
            {
                Id = id,
                CorpId = "corp-form-data-delete",
                AppId = "app-1",
                FormId = "form-1",
                FlowStatus = flowStatus,
                DeleteFlag = deleteFlag
            };
        }

        private sealed class TestableFormDataService : FormDataService
        {
            private IReadOnlyList<FormData> _deleteTargets = [];
            private readonly RecordingRepository<AuditLog> _auditLogRepository;
            private readonly QueryableRepository<FormData> _formDataRepository;

            private TestableFormDataService(RecordingRepository<AuditLog> auditLogRepository, QueryableRepository<FormData> formDataRepository) : base(BuildResolver(auditLogRepository, formDataRepository))
            {
                _auditLogRepository = auditLogRepository;
                _formDataRepository = formDataRepository;
            }

            public List<string> LogicDeletedIds { get; } = [];
            public List<string> HardDeletedIds { get; } = [];
            public List<string> RelatedDeletedIds { get; } = [];
            public List<string> WorkflowDeletedDataIds { get; } = [];
            public IReadOnlyList<AuditLog> AuditLogs => _auditLogRepository.Items;

            public static TestableFormDataService Create()
            {
                return new TestableFormDataService(new RecordingRepository<AuditLog>(), new QueryableRepository<FormData>());
            }

            /// <summary>
            /// <c>PurgeCoreAsync</c> 直接查询 <see cref="IRepository{T}.Queryable"/>（并要求
            /// <c>IgnoreQueryFilters()</c>），因此目标的写入必须同时落到真实可查询的仓储上。
            /// </summary>
            private IReadOnlyList<FormData> DeleteTargets
            {
                get => _deleteTargets;
                set
                {
                    _deleteTargets = value;
                    _formDataRepository.SetItems(value);
                }
            }

            public void InvokeDeleteCore(IReadOnlyList<FormData> targets)
            {
                DeleteTargets = targets;
                DeleteCore(x => true);
            }

            public Task<int> InvokeDeleteCoreAsync(IReadOnlyList<FormData> targets)
            {
                DeleteTargets = targets;
                return DeleteCoreAsync(x => true);
            }

            public void InvokeEnsureCanEdit(FormData entity, bool usingWorkflow)
            {
                EnsureCanEdit(entity, new FormDef { UsingWorkflow = usingWorkflow });
            }

            public Task InvokePurgeCoreAsync(IEnumerable<string> requestedIds, IReadOnlyList<FormData> targets)
            {
                DeleteTargets = targets;
                return PurgeCoreAsync(requestedIds.ToList());
            }

            protected override IReadOnlyList<FormData> FindDeleteTargets(Expression<Func<FormData, bool>> filter)
            {
                return _deleteTargets;
            }

            protected override Task<IReadOnlyList<FormData>> FindDeleteTargetsAsync(Expression<Func<FormData, bool>> filter)
            {
                return Task.FromResult(_deleteTargets);
            }

            protected override long DeleteFormDataByIds(IReadOnlyCollection<string> ids, bool physical)
            {
                if (physical)
                {
                    HardDeletedIds.AddRange(ids);
                }
                else
                {
                    LogicDeletedIds.AddRange(ids);
                }

                return ids.Count;
            }

            protected override Task<long> DeleteFormDataByIdsAsync(IReadOnlyCollection<string> ids, bool physical)
            {
                return Task.FromResult(DeleteFormDataByIds(ids, physical));
            }

            protected override void DeleteStronglyRelatedData(IReadOnlyCollection<string> dataIds)
            {
                RelatedDeletedIds.AddRange(dataIds);
            }

            protected override Task DeleteStronglyRelatedDataAsync(IReadOnlyCollection<string> dataIds)
            {
                RelatedDeletedIds.AddRange(dataIds);
                return Task.CompletedTask;
            }

            protected override Task<WfResponse?> DeleteWorkflowInstancesByDataIdsAsync(IReadOnlyCollection<string> dataIds)
            {
                WorkflowDeletedDataIds.AddRange(dataIds);
                return Task.FromResult<WfResponse?>(new WfResponse { Id = string.Join(",", dataIds) });
            }

            private static IResolver BuildResolver(RecordingRepository<AuditLog> auditLogRepository, QueryableRepository<FormData> formDataRepository)
            {
                var scopeCache = new FakeScopeCache();
                var context = new FakeServiceContext
                {
                    CorpId = "corp-form-data-delete",
                    Operator = new Operator("operator-1", "OP1", "Operator One"),
                    ClientIp = "127.0.0.1",
                    ScopeCache = scopeCache
                };
                var services = new Dictionary<Type, object>
                {
                    [typeof(IServiceContext)] = context,
                    [typeof(IScopeCache)] = scopeCache,
                    [typeof(IRepository<FormData>)] = formDataRepository,
                    [typeof(IRepository<AuditLog>)] = auditLogRepository,
                    [typeof(ICacheClient)] = new FakeCacheClient(),
                    [typeof(ILogger<FormData>)] = NullLogger<FormData>.Instance,
                    [typeof(FlowApiClient)] = CreateFlowClient(),
                    [typeof(ISerialNoSequenceService)] = new FakeSerialNoSequenceService()
                };
                return new TestResolver(services);
            }

            private static FlowApiClient CreateFlowClient()
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["FlowApiClient:BaseUrl"] = "http://localhost"
                    })
                    .Build();
                return new FlowApiClient(config, NullLogger<FlowApiClient>.Instance);
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

        private sealed class FakeServiceContext : IServiceContext
        {
            public string AccessToken { get; set; } = string.Empty;
            public string CorpId { get; set; } = string.Empty;
            public Operator? Operator { get; set; }
            public string UserId { get; set; } = string.Empty;
            public IUser? User { get; set; }
            public IEmployee? Employee { get; set; }
            public string? ClientIp { get; set; }
            public DataAction Action { get; set; }
            public IScopeCache ScopeCache { get; set; } = new FakeScopeCache();
        }

        private sealed class FakeScopeCache : IScopeCache
        {
            public IEnumerable<T> GetAll<T>(DataVersion version = DataVersion.Temp) where T : class => [];
            public T? Get<T>(string key, DataVersion version = DataVersion.Temp, Func<string, T?>? getter = null) where T : class => null;
            public void Set<T>(string key, T value, DataVersion version = DataVersion.Temp) where T : class { }
            public void Remove<T>(string key, DataVersion version = DataVersion.Temp) where T : class { }
            public bool Contains<T>(string key, DataVersion version = DataVersion.Temp) where T : class => false;
        }

        private sealed class FakeCacheClient : ICacheClient
        {
            public string? GetString(string key, CacheScope scope, string scopeId = "") => null;
            public Task<string?> GetStringAsync(string key, CacheScope scope, string scopeId = "") => Task.FromResult<string?>(null);
            public T? Get<T>(string key, CacheScope scope, string scopeId = "") => default;
            public Task<T?> GetAsync<T>(string key, CacheScope scope, string scopeId = "") => Task.FromResult<T?>(default);
            public void SetString(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) { }
            public Task SetStringAsync(string key, string value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) => Task.CompletedTask;
            public void Set<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) { }
            public Task SetAsync<T>(string key, T value, CacheScope scope, string scopeId = "", DistributedCacheEntryOptions? options = null) => Task.CompletedTask;
            public void Refresh(string key, CacheScope scope, string scopeId = "") { }
            public Task RefreshAsync(string key, CacheScope scope, string scopeId = "") => Task.CompletedTask;
            public void Remove(string key, CacheScope scope, string scopeId = "") { }
            public Task RemoveAsync(string key, CacheScope scope, string scopeId = "") => Task.CompletedTask;
            public long Increment(string key, long delta, TimeSpan ttl, CacheScope scope, string scopeId = "") => 0;
            public Task<long> IncrementAsync(string key, long delta, TimeSpan ttl, CacheScope scope, string scopeId = "") => Task.FromResult(0L);
            public Task<bool> TrySetStringAsync(string key, string value, TimeSpan ttl, CacheScope scope, string scopeId = "") => Task.FromResult(true);
        }

        private sealed class FakeSerialNoSequenceService : StubEntityService<SerialNoSequence>, ISerialNoSequenceService
        {
            public int NextFormSerialNo(string corpId, string appId, string formId, string key, SerialNoResetCycle cycle) => throw new NotSupportedException();
        }

        /// <summary>
        /// 这些成员由共享的 <see cref="StubEntityService{T}"/> 统一兜底。
        /// </summary>
        private sealed class QueryableRepository<T> : StubRepository<T> where T : class, IEntity
        {
            private List<T> _items = [];

            public void SetItems(IEnumerable<T> items) => _items = items.ToList();

            // PurgeCoreAsync 走的是 Repository.Queryable.IgnoreQueryFilters()…ToListAsync()，
            // 需要源同时具备同步与异步可枚举能力，故用 InMemoryAsyncQueryable 包装。
            public override IQueryable<T> Queryable => new InMemoryAsyncQueryable<T>(_items);

            public override IQueryable<T> Find(DynamicFilter filter) => Queryable;

            public override List<T> FindList(DynamicFilter filter) => Queryable.ToList();

            public override T? Get(string id) => _items.FirstOrDefault(x => x.Id == id);

            public override Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(id));

            public override long Count(Expression<Func<T, bool>> predicate) => Queryable.LongCount(predicate);

            public override long Count(DynamicFilter filter) => Queryable.LongCount();

            public override Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
                => Task.FromResult(Count(predicate));

            public override Task<int> SoftDeleteManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
                => Task.FromResult(SoftDeleteMany(ids));

            public override Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            {
                var removed = Queryable.Where(predicate).ToList();
                _items.RemoveAll(removed.Contains);
                return Task.FromResult(removed.Count);
            }

            private int SoftDeleteMany(IEnumerable<string> ids)
            {
                var idSet = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var affected = _items.Where(x => idSet.Contains(x.Id)).ToList();
                foreach (var item in affected)
                {
                    item.DeleteFlag = true;
                }

                return affected.Count;
            }
        }

        private sealed class RecordingRepository<T> : StubRepository<T> where T : class, IEntityKey
        {
            private readonly List<T> _items = [];

            public IReadOnlyList<T> Items => _items;

            public override IQueryable<T> Queryable => _items.AsQueryable();

            public override void Insert(T entity) => _items.Add(entity);

            public override Task InsertAsync(T entity, CancellationToken cancellationToken = default)
            {
                Insert(entity);
                return Task.CompletedTask;
            }

            public override Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
            {
                _items.AddRange(entities);
                return Task.CompletedTask;
            }

            public override string NewId() => Guid.NewGuid().ToString("N");
        }
    }
}
