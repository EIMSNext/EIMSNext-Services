using System.Composition.Hosting;
using System.Linq.Expressions;
using System.Text.Json;

using EIMSNext.ApiClient.Flow;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

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

            private TestableFormDataService(RecordingRepository<AuditLog> auditLogRepository) : base(BuildResolver(auditLogRepository))
            {
                _auditLogRepository = auditLogRepository;
            }

            public List<string> LogicDeletedIds { get; } = [];
            public List<string> HardDeletedIds { get; } = [];
            public List<string> RelatedDeletedIds { get; } = [];
            public List<string> WorkflowDeletedDataIds { get; } = [];
            public IReadOnlyList<AuditLog> AuditLogs => _auditLogRepository.Items;

            public static TestableFormDataService Create()
            {
                return new TestableFormDataService(new RecordingRepository<AuditLog>());
            }

        public void InvokeDeleteCore(IReadOnlyList<FormData> targets)
        {
            _deleteTargets = targets;
            DeleteCore(Builders<FormData>.Filter.Empty, null);
        }

        public Task<object> InvokeDeleteCoreAsync(IReadOnlyList<FormData> targets)
        {
            _deleteTargets = targets;
            return DeleteCoreAsync(Builders<FormData>.Filter.Empty, null);
        }

        public void InvokeEnsureCanEdit(FormData entity, bool usingWorkflow)
        {
            EnsureCanEdit(entity, new FormDef { UsingWorkflow = usingWorkflow });
        }

            public Task InvokePurgeCoreAsync(IEnumerable<string> requestedIds, IReadOnlyList<FormData> targets)
            {
                _deleteTargets = targets;
                return PurgeCoreAsync(requestedIds.ToList(), null);
            }

            protected override IReadOnlyList<FormData> FindDeleteTargets(FilterDefinition<FormData> filter, IClientSessionHandle? session)
            {
                return _deleteTargets;
            }

            protected override Task<IReadOnlyList<FormData>> FindDeleteTargetsAsync(FilterDefinition<FormData> filter, IClientSessionHandle? session)
            {
                return Task.FromResult(_deleteTargets);
            }

            protected override long DeleteFormDataByIds(IReadOnlyCollection<string> ids, bool physical, IClientSessionHandle? session)
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

            protected override Task<long> DeleteFormDataByIdsAsync(IReadOnlyCollection<string> ids, bool physical, IClientSessionHandle? session)
            {
                return Task.FromResult(DeleteFormDataByIds(ids, physical, session));
            }

            protected override void DeleteStronglyRelatedData(IReadOnlyCollection<string> dataIds, IClientSessionHandle? session)
            {
                RelatedDeletedIds.AddRange(dataIds);
            }

            protected override Task DeleteStronglyRelatedDataAsync(IReadOnlyCollection<string> dataIds, IClientSessionHandle? session)
            {
                RelatedDeletedIds.AddRange(dataIds);
                return Task.CompletedTask;
            }

            protected override Task<WfResponse?> DeleteWorkflowInstancesByDataIdsAsync(IReadOnlyCollection<string> dataIds)
            {
                WorkflowDeletedDataIds.AddRange(dataIds);
                return Task.FromResult<WfResponse?>(new WfResponse { Id = string.Join(",", dataIds) });
            }

            private static IResolver BuildResolver(RecordingRepository<AuditLog> auditLogRepository)
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
                    [typeof(IRepository<FormData>)] = new StubRepository<FormData>(),
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
        }

        private sealed class FakeSerialNoSequenceService : ISerialNoSequenceService
        {
            public IMongoCollection<SerialNoSequence> Collection => throw new NotSupportedException();
            public string NextCorpCode(PlatformType platform) => throw new NotSupportedException();
            public int NextFormSerialNo(string corpId, string appId, string formId, string key, SerialNoResetCycle cycle) => throw new NotSupportedException();
            public SerialNoSequence? Get(string id) => throw new NotSupportedException();
            public IQueryable<SerialNoSequence> All() => throw new NotSupportedException();
            public IQueryable<SerialNoSequence> Query(Expression<Func<SerialNoSequence, bool>> where) => throw new NotSupportedException();
            public IFindFluent<SerialNoSequence, SerialNoSequence> Find(DynamicFindOptions<SerialNoSequence> options) => throw new NotSupportedException();
            public IFindFluent<SerialNoSequence, SerialNoSequence> Find(Expression<Func<SerialNoSequence, bool>> filter) => throw new NotSupportedException();
            public long Count(DynamicFilter filter) => throw new NotSupportedException();
            public long Count(Expression<Func<SerialNoSequence, bool>> filter) => throw new NotSupportedException();
            public bool Exists(Expression<Func<SerialNoSequence, bool>> where) => throw new NotSupportedException();
            public bool Exists(DynamicFilter where) => throw new NotSupportedException();
            public void Add(SerialNoSequence entity) => throw new NotSupportedException();
            public void Add(IEnumerable<SerialNoSequence> entities) => throw new NotSupportedException();
            public ReplaceOneResult Replace(SerialNoSequence entity) => throw new NotSupportedException();
            public object Delete(string id) => throw new NotSupportedException();
            public object Delete(IEnumerable<string> ids) => throw new NotSupportedException();
            public object Delete(DynamicFilter filter) => throw new NotSupportedException();
            public Task<SerialNoSequence?> GetAsync(string id) => throw new NotSupportedException();
            public Task<IAsyncCursor<SerialNoSequence>> FindAsync(DynamicFindOptions<SerialNoSequence> options) => throw new NotSupportedException();
            public Task<IAsyncCursor<SerialNoSequence>> FindAsync(Expression<Func<SerialNoSequence, bool>> filter) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter) => throw new NotSupportedException();
            public Task<long> CountAsync(Expression<Func<SerialNoSequence, bool>> filter) => throw new NotSupportedException();
            public Task<bool> ExistsAsync(Expression<Func<SerialNoSequence, bool>> where) => throw new NotSupportedException();
            public Task<bool> ExistsAsync(DynamicFilter where) => throw new NotSupportedException();
            public Task AddAsync(SerialNoSequence entity) => throw new NotSupportedException();
            public Task AddAsync(IEnumerable<SerialNoSequence> entities) => throw new NotSupportedException();
            public Task<ReplaceOneResult> ReplaceAsync(SerialNoSequence entity) => throw new NotSupportedException();
            public Task<object> DeleteAsync(string id) => throw new NotSupportedException();
            public Task<object> DeleteAsync(IEnumerable<string> ids) => throw new NotSupportedException();
            public Task<object> DeleteAsync(DynamicFilter filter) => throw new NotSupportedException();
        }

        private sealed class StubRepository<T> : IRepository<T> where T : class, IMongoEntity
        {
            public IMongoDbContex DbContext => throw new NotSupportedException();
            public IMongoCollection<T> Collection => throw new NotSupportedException();
            public IQueryable<T> Queryable => throw new NotSupportedException();
            public FilterDefinitionBuilder<T> FilterBuilder => Builders<T>.Filter;
            public SortDefinitionBuilder<T> SortBuilder => Builders<T>.Sort;
            public SearchDefinitionBuilder<T> SearchBuilder => Builders<T>.Search;
            public ProjectionDefinitionBuilder<T> ProjectionBuilder => Builders<T>.Projection;
            public UpdateDefinitionBuilder<T> UpdateBuilder => Builders<T>.Update;

            public MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public T? Get(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<T?> GetAsync(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public void Insert(T entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(T entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task InsertAsync(IEnumerable<T> entities, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult Update(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateManyAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<List<BsonValue>> DistinctFieldValuesAsync(DynamicFilter filter, string field, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IEnumerable<T> EnsureId(IEnumerable<T> entities) => throw new NotSupportedException();
            public T EnsureId(T entity) => throw new NotSupportedException();
            public string NewId() => throw new NotSupportedException();
        }

        private sealed class RecordingRepository<T> : IRepository<T> where T : class, IMongoEntity
        {
            private readonly List<T> _items = [];

            public IReadOnlyList<T> Items => _items;
            public IMongoDbContex DbContext => throw new NotSupportedException();
            public IMongoCollection<T> Collection => throw new NotSupportedException();
            public IQueryable<T> Queryable => _items.AsQueryable();
            public FilterDefinitionBuilder<T> FilterBuilder => Builders<T>.Filter;
            public SortDefinitionBuilder<T> SortBuilder => Builders<T>.Sort;
            public SearchDefinitionBuilder<T> SearchBuilder => Builders<T>.Search;
            public ProjectionDefinitionBuilder<T> ProjectionBuilder => Builders<T>.Projection;
            public UpdateDefinitionBuilder<T> UpdateBuilder => Builders<T>.Update;
            public MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public T? Get(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<T?> GetAsync(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public void Insert(T entity, IClientSessionHandle? session = null) => _items.Add(entity);
            public void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null) => _items.AddRange(entities);
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
            public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<List<BsonValue>> DistinctFieldValuesAsync(DynamicFilter filter, string field, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public IEnumerable<T> EnsureId(IEnumerable<T> entities) => entities;
            public T EnsureId(T entity) => entity;
            public string NewId() => ObjectId.GenerateNewId().ToString();
        }
    }
}
