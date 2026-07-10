using System.Linq.Expressions;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Core.MongoDb;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.MongoDb;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class FormDataImportLogServiceStateMachineTests
    {
        private const string LogId = "log-1";

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            // 注册 BsonClassMap，确保 Render 序列化器能正确解析 FormDataImportLog
            BsonClassMap.TryRegisterClassMap<FormDataImportLog>(cm =>
            {
                cm.AutoMap();
            });
        }

        [TestMethod]
        public async Task TryMarkProcessingAsync_RequiresPendingStatusAndRetryCount()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            var acquired = await service.TryMarkProcessingAsync(LogId, retryCount: 2);

            Assert.IsTrue(acquired);
            var filter = Render(repo.LastFilter!);
            var update = Render(repo.LastUpdate!);
            var idValue = filter.TryGetValue("Id", out var explicitId) ? explicitId : filter["_id"];
            Assert.AreEqual(LogId, idValue.AsString);
            var filterJson = filter.ToJson();
            StringAssert.Contains(filterJson, "RetryCount");
            StringAssert.Contains(filterJson, "Status");
            Assert.IsFalse(filterJson.Contains("$or"));
            Assert.IsFalse(filterJson.Contains("ProcessingExpireTime"));
            Assert.AreEqual((int)FormDataImportStatus.Processing, update["$set"]["Status"].ToInt32());
            Assert.AreEqual(0L, update["$set"]["TotalCount"].ToInt64());
            Assert.IsTrue(update["$set"].AsBsonDocument.Contains("ProcessingExpireTime"));
            Assert.IsFalse(repo.LastUpsert);
        }

        [TestMethod]
        public async Task TryMarkProcessingAsync_ReturnsFalseWhenStateNotAcquired()
        {
            var repo = new RecordingRepository<FormDataImportLog> { ModifiedCount = 0 };
            var service = NewService(repo);

            var acquired = await service.TryMarkProcessingAsync(LogId, retryCount: 2);

            Assert.IsFalse(acquired);
        }

        [TestMethod]
        public async Task MarkProcessingAsync_SetsStatusAndResetsCounters()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkProcessingAsync(LogId, totalCount: 100);

            var doc = Render(repo.LastUpdate!);
            var set = doc["$set"].AsBsonDocument;
            Assert.AreEqual((int)FormDataImportStatus.Processing, set["Status"].ToInt32());
            Assert.AreEqual(100L, set["TotalCount"].ToInt64());
            Assert.AreEqual(0L, set["ProcessedCount"].ToInt64());
            Assert.AreEqual(0L, set["AddCount"].ToInt64());
            Assert.AreEqual(0L, set["UpdateCount"].ToInt64());
            Assert.AreEqual(0L, set["FailedCount"].ToInt64());
            Assert.IsTrue(set.Contains("StartTime"));
            Assert.IsTrue(set["FinishTime"].IsBsonNull);
            Assert.IsTrue(set.Contains("ProcessingExpireTime"));
            Assert.IsTrue(set["ErrorMessage"].IsBsonNull);
        }

        [TestMethod]
        public async Task UpdateProgressAsync_OnlySetsCounters()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.UpdateProgressAsync(LogId, processedCount: 20, addCount: 15, updateCount: 5, failedCount: 0);

            var set = Render(repo.LastUpdate!)["$set"].AsBsonDocument;
            Assert.AreEqual(20L, set["ProcessedCount"].ToInt64());
            Assert.AreEqual(15L, set["AddCount"].ToInt64());
            Assert.AreEqual(5L, set["UpdateCount"].ToInt64());
            Assert.AreEqual(0L, set["FailedCount"].ToInt64());
            Assert.IsTrue(set.Contains("ProcessingExpireTime"));
            Assert.IsFalse(set.Contains("status"));
            Assert.IsFalse(set.Contains("totalCount"));
        }

        [TestMethod]
        public async Task MarkSucceededAsync_SetsTerminalStatusAndClearsEditableErrors()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkSucceededAsync(LogId, totalCount: 50, addCount: 30, updateCount: 20);

            var set = Render(repo.LastUpdate!)["$set"].AsBsonDocument;
            Assert.AreEqual((int)FormDataImportStatus.Succeeded, set["Status"].ToInt32());
            Assert.AreEqual(50L, set["TotalCount"].ToInt64());
            Assert.AreEqual(50L, set["ProcessedCount"].ToInt64());
            Assert.AreEqual(30L, set["AddCount"].ToInt64());
            Assert.AreEqual(20L, set["UpdateCount"].ToInt64());
            Assert.AreEqual(0L, set["FailedCount"].ToInt64());
            Assert.IsTrue(set["EditableErrorRowsJson"].IsBsonNull);
            Assert.IsTrue(set["EditableErrorRowsObjectKey"].IsBsonNull);
            Assert.AreEqual(0, set["EditableErrorRowCount"].ToInt32());
            Assert.IsTrue(set["ErrorMessage"].IsBsonNull);
            Assert.IsTrue(set.Contains("FinishTime"));
        }

        [TestMethod]
        public async Task MarkCompletedWithErrorsAsync_PersistsReportAndEditableRows()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkCompletedWithErrorsAsync(
                LogId,
                totalCount: 100, addCount: 80, updateCount: 10, failedCount: 10,
                errorReportFileName: "r.xlsx", errorReportObjectKey: "k", errorReportDownloadUrl: "https://x",
                editableErrorRowsJson: "[]", editableErrorRowsObjectKey: null, editableErrorRowCount: 5);

            var set = Render(repo.LastUpdate!)["$set"].AsBsonDocument;
            Assert.AreEqual((int)FormDataImportStatus.CompletedWithErrors, set["Status"].ToInt32());
            Assert.AreEqual(100L, set["TotalCount"].ToInt64());
            Assert.AreEqual(100L, set["ProcessedCount"].ToInt64());
            Assert.AreEqual(80L, set["AddCount"].ToInt64());
            Assert.AreEqual(10L, set["UpdateCount"].ToInt64());
            Assert.AreEqual(10L, set["FailedCount"].ToInt64());
            Assert.AreEqual("r.xlsx", set["ErrorReportFileName"].AsString);
            Assert.AreEqual("k", set["ErrorReportObjectKey"].AsString);
            Assert.AreEqual("https://x", set["ErrorReportDownloadUrl"].AsString);
            Assert.AreEqual("[]", set["EditableErrorRowsJson"].AsString);
            Assert.IsTrue(set["EditableErrorRowsObjectKey"].IsBsonNull);
            Assert.AreEqual(5, set["EditableErrorRowCount"].ToInt32());
        }

        [TestMethod]
        public async Task MarkFailedAsync_SetsStatusErrorAndClearsEditableErrors()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkFailedAsync(LogId, "boom",
                errorReportFileName: "f.xlsx", errorReportObjectKey: "fk", errorReportDownloadUrl: "https://f");

            var set = Render(repo.LastUpdate!)["$set"].AsBsonDocument;
            Assert.AreEqual((int)FormDataImportStatus.Failed, set["Status"].ToInt32());
            Assert.AreEqual("boom", set["ErrorMessage"].AsString);
            Assert.AreEqual("f.xlsx", set["ErrorReportFileName"].AsString);
            Assert.AreEqual("fk", set["ErrorReportObjectKey"].AsString);
            Assert.AreEqual("https://f", set["ErrorReportDownloadUrl"].AsString);
            Assert.IsTrue(set["EditableErrorRowsJson"].IsBsonNull);
            Assert.IsTrue(set["EditableErrorRowsObjectKey"].IsBsonNull);
            Assert.AreEqual(0, set["EditableErrorRowCount"].ToInt32());
            Assert.IsTrue(set.Contains("FinishTime"));
        }

        [TestMethod]
        public async Task MarkCorrectionResultAsync_WithErrors_PersistsRemainingEditableRowsAndClearsReport()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkCorrectionResultAsync(LogId, totalCount: 5, addCount: 2, updateCount: 1, failedCount: 2, editableErrorRowsJson: "[1,2]", editableErrorRowsObjectKey: null, editableErrorRowCount: 2);

            var set = Render(repo.LastUpdate!)["$set"].AsBsonDocument;
            Assert.AreEqual((int)FormDataImportStatus.CompletedWithErrors, set["Status"].ToInt32());
            Assert.AreEqual(5L, set["TotalCount"].ToInt64());
            Assert.AreEqual(5L, set["ProcessedCount"].ToInt64());
            Assert.AreEqual(2L, set["AddCount"].ToInt64());
            Assert.AreEqual(1L, set["UpdateCount"].ToInt64());
            Assert.AreEqual(2L, set["FailedCount"].ToInt64());
            Assert.AreEqual("[1,2]", set["EditableErrorRowsJson"].AsString);
            Assert.IsTrue(set["EditableErrorRowsObjectKey"].IsBsonNull);
            Assert.AreEqual(2, set["EditableErrorRowCount"].ToInt32());
            Assert.IsTrue(set["ErrorReportFileName"].IsBsonNull);
            Assert.IsTrue(set["ErrorReportObjectKey"].IsBsonNull);
            Assert.IsTrue(set["ErrorReportDownloadUrl"].IsBsonNull);
            Assert.IsTrue(set["ErrorMessage"].IsBsonNull);
            Assert.IsTrue(set.Contains("FinishTime"));
        }

        [TestMethod]
        public async Task MarkCorrectionResultAsync_WithoutErrors_SetsSucceededAndClearsEditableRows()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.MarkCorrectionResultAsync(LogId, totalCount: 3, addCount: 1, updateCount: 2, failedCount: 0, editableErrorRowsJson: null, editableErrorRowsObjectKey: null, editableErrorRowCount: 0);

            var set = Render(repo.LastUpdate!)["$set"].AsBsonDocument;
            Assert.AreEqual((int)FormDataImportStatus.Succeeded, set["Status"].ToInt32());
            Assert.AreEqual(0, set["EditableErrorRowCount"].ToInt32());
            Assert.IsTrue(set["EditableErrorRowsJson"].IsBsonNull);
            Assert.IsTrue(set["ErrorReportDownloadUrl"].IsBsonNull);
        }

        [TestMethod]
        public async Task UpdateEditableErrorsAsync_OnlyUpdatesEditableFields()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.UpdateEditableErrorsAsync(LogId, "[1]", null, 3);

            var set = Render(repo.LastUpdate!)["$set"].AsBsonDocument;
            Assert.AreEqual("[1]", set["EditableErrorRowsJson"].AsString);
            Assert.IsTrue(set["EditableErrorRowsObjectKey"].IsBsonNull);
            Assert.AreEqual(3, set["EditableErrorRowCount"].ToInt32());
            Assert.IsFalse(set.Contains("status"));
        }

        [TestMethod]
        public async Task IncrementRetryAsync_OnlyIncrementsRetryCount()
        {
            var repo = new RecordingRepository<FormDataImportLog>();
            var service = NewService(repo);

            await service.IncrementRetryAsync(LogId);

            var doc = Render(repo.LastUpdate!);
            Assert.IsFalse(doc.Contains("$set"));
            var inc = doc["$inc"].AsBsonDocument;
            Assert.AreEqual(1, inc.GetValue("RetryCount", -1).ToInt32(), $"got: {doc.ToJson()}");
        }

        private static IFormDataImportLogService NewService(IRepository<FormDataImportLog> repo)
        {
            var resolver = new TestResolver(repo);
            return new FormDataImportLogService(resolver);
        }

        private static BsonDocument Render(UpdateDefinition<FormDataImportLog> update)
        {
            var registry = BsonSerializer.SerializerRegistry;
            var serializer = registry.GetSerializer<FormDataImportLog>();
            return update.Render(new RenderArgs<FormDataImportLog>(serializer, registry)).ToBsonDocument();
        }

        private static BsonDocument Render(FilterDefinition<FormDataImportLog> filter)
        {
            var registry = BsonSerializer.SerializerRegistry;
            var serializer = registry.GetSerializer<FormDataImportLog>();
            return filter.Render(new RenderArgs<FormDataImportLog>(serializer, registry)).ToBsonDocument();
        }

        private sealed class TestResolver : IResolver
        {
            private readonly Dictionary<Type, object> _services = new();

            public TestResolver(IRepository<FormDataImportLog> repo)
            {
                _services[typeof(IRepository<FormDataImportLog>)] = repo;
                _services[typeof(IRepository<AuditLog>)] = new StubRepository<AuditLog>();
                _services[typeof(ICacheClient)] = new FakeCacheClient();
                _services[typeof(IServiceContext)] = new FakeServiceContext();
                _services[typeof(IScopeCache)] = new FakeScopeCache();
                _services[typeof(ILogger<FormDataImportLog>)] = NullLogger<FormDataImportLog>.Instance;
            }

            public CompositionContainer MefContainer => throw new NotSupportedException();
            public object Resolve(Type type, string? name = null) => _services[type];
            public T Resolve<T>(string? name = null) where T : class => (T)_services[typeof(T)];
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

        private sealed class StubRepository<T> : IRepository<T> where T : class, IMongoEntity
        {
            public IMongoDbContex DbContext => throw new NotSupportedException();
            public IMongoCollection<T> Collection => throw new NotSupportedException();
            public IQueryable<T> Queryable => throw new NotSupportedException();
            public FilterDefinitionBuilder<T> FilterBuilder => throw new NotSupportedException();
            public SortDefinitionBuilder<T> SortBuilder => throw new NotSupportedException();
            public SearchDefinitionBuilder<T> SearchBuilder => throw new NotSupportedException();
            public ProjectionDefinitionBuilder<T> ProjectionBuilder => throw new NotSupportedException();
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
            public Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult Update(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
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

        private sealed class RecordingRepository<T> : IRepository<T> where T : class, IMongoEntity
        {
            public FilterDefinition<T>? LastFilter { get; private set; }
            public UpdateDefinition<T>? LastUpdate { get; private set; }
            public string? LastUpdateId { get; private set; }
            public bool LastUpsert { get; private set; }
            public long ModifiedCount { get; set; } = 1;

            public Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
            {
                LastUpdate = update;
                LastUpdateId = id;
                LastUpsert = upsert;
                return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(1, ModifiedCount, new BsonDocument()));
            }

            public IMongoDbContex DbContext => throw new NotSupportedException();
            public IMongoCollection<T> Collection => throw new NotSupportedException();
            public IQueryable<T> Queryable => throw new NotSupportedException();
            public FilterDefinitionBuilder<T> FilterBuilder => Builders<T>.Filter;
            public SortDefinitionBuilder<T> SortBuilder => throw new NotSupportedException();
            public SearchDefinitionBuilder<T> SearchBuilder => throw new NotSupportedException();
            public ProjectionDefinitionBuilder<T> ProjectionBuilder => throw new NotSupportedException();
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
            public UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
            {
                LastFilter = filter;
                LastUpdate = update;
                LastUpsert = upsert;
                return new UpdateResult.Acknowledged(1, ModifiedCount, new BsonDocument());
            }
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
