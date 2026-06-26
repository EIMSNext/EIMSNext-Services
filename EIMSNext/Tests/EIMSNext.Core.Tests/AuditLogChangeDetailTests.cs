using System.Composition.Hosting;
using System.Linq.Expressions;
using System.Reflection;
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
using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public class AuditLogChangeDetailTests
    {
        [TestMethod]
        public void T1_KeyRemovedInNew_DoesNotThrow_AndContainsDeletedLine()
        {
            var service = NewService();

            var oldE = new TestAuditEntity { A = "1", B = "2", C = "3" };
            var newE = new TestAuditEntity { A = "1", B = "2" };

            var result = InvokeGetChangeDetail(service, oldE, newE);

            Assert.IsFalse(result.Contains("KeyNotFound", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(result.Contains("C:"), $"expected C: line in '{result}'");
            Assert.IsTrue(result.Contains("->null"), $"expected '->null' suffix in '{result}'");
        }

        [TestMethod]
        public void T2_KeyAddedInNew_ContainsAddedLine()
        {
            var service = NewService();

            var oldE = new TestAuditEntity { A = "1" };
            var newE = new TestAuditEntity { A = "1", D = "new-field" };

            var result = InvokeGetChangeDetail(service, oldE, newE);

            Assert.IsTrue(result.Contains("D:"), $"expected D: line in '{result}'");
            Assert.IsTrue(result.StartsWith("D:null->", StringComparison.Ordinal)
                || result.Contains(",D:null->", StringComparison.Ordinal),
                $"expected 'D:null->...' in '{result}'");
            Assert.IsFalse(result.Contains("A:"), $"A should be skipped (unchanged) in '{result}'");
        }

        [TestMethod]
        public void T3_AllFieldsEqual_ReturnsEmptyString()
        {
            var service = NewService();

            var oldE = new TestAuditEntity { A = "1", B = "2" };
            var newE = new TestAuditEntity { A = "1", B = "2" };

            var result = InvokeGetChangeDetail(service, oldE, newE);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void T3b_ChangedField_LineUsesJsonStrings()
        {
            var service = NewService();

            var oldE = new TestAuditEntity { A = "before" };
            var newE = new TestAuditEntity { A = "after" };

            var result = InvokeGetChangeDetail(service, oldE, newE);

            Assert.IsTrue(result.Contains("A:\"before\"->\"after\""),
                $"expected A:\"before\"->\"after\" in '{result}'");
        }

        [TestMethod]
        public void T3c_MultipleChanges_OnlyChangedFieldsAppear()
        {
            var service = NewService();

            var oldE = new TestAuditEntity { A = "1", B = "2", C = "3" };
            var newE = new TestAuditEntity { A = "1", B = "20", C = "30" };

            var result = InvokeGetChangeDetail(service, oldE, newE);

            Assert.IsTrue(result.Contains("B:\"2\"->\"20\""), $"expected B line in '{result}'");
            Assert.IsTrue(result.Contains("C:\"3\"->\"30\""), $"expected C line in '{result}'");
            Assert.IsFalse(result.Contains("A:"), $"A unchanged should be skipped in '{result}'");
        }

        private static TestEntityService<TestAuditEntity> NewService()
            => new TestEntityService<TestAuditEntity>(BuildResolver());

        private static string InvokeGetChangeDetail(TestEntityService<TestAuditEntity> service, TestAuditEntity oldT, TestAuditEntity newT)
        {
            // GetChangeDetail 是 ServiceCore<T> 上的私有方法，使用 closed generic 类型的 MethodInfo
            var method = typeof(ServiceCore<TestAuditEntity>).GetMethod("GetChangeDetail", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("GetChangeDetail not found on ServiceCore<TestAuditEntity>");
            return (string)method.Invoke(service, [oldT, newT])!;
        }

        private static IResolver BuildResolver()
        {
            var context = new FakeServiceContext
            {
                CorpId = "test-corp",
                Operator = new Operator("op-1", "OP1", "Operator One"),
                ClientIp = "1.2.3.4"
            };
            var services = new Dictionary<Type, object>
            {
                [typeof(IServiceContext)] = context,
                [typeof(IScopeCache)] = new FakeScopeCache(),
                [typeof(IRepository<TestAuditEntity>)] = new StubRepository<TestAuditEntity>(),
                [typeof(IRepository<AuditLog>)] = new StubRepository<AuditLog>(),
                [typeof(ICacheClient)] = new FakeCacheClient(),
                [typeof(ILogger<TestAuditEntity>)] = NullLogger<TestAuditEntity>.Instance
            };
            return new TestResolver(services);
        }

        private sealed class TestAuditEntity : CorpEntityBase
        {
            public string? A { get; set; }
            public string? B { get; set; }
            public string? C { get; set; }
            public string? D { get; set; }
        }

        private sealed class TestEntityService<T> : MongoEntityServiceBase<T> where T : class, IMongoEntity
        {
            public TestEntityService(IResolver resolver) : base(resolver) { }
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

        // 最小桩：仅让 ServiceCore 构造成功；任何实际方法被调用都抛异常（GetChangeDetail 不触发）。
        private sealed class StubRepository<T> : IRepository<T> where T : class, IMongoEntity
        {
            public IMongoDbContex DbContext => throw new NotSupportedException();
            public IMongoCollection<T> Collection => throw new NotSupportedException();
            public IQueryable<T> Queryable => throw new NotSupportedException();
            public FilterDefinitionBuilder<T> FilterBuilder => throw new NotSupportedException();
            public SortDefinitionBuilder<T> SortBuilder => throw new NotSupportedException();
            public SearchDefinitionBuilder<T> SearchBuilder => throw new NotSupportedException();
            public ProjectionDefinitionBuilder<T> ProjectionBuilder => throw new NotSupportedException();
            public UpdateDefinitionBuilder<T> UpdateBuilder => throw new NotSupportedException();
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
    }
}
