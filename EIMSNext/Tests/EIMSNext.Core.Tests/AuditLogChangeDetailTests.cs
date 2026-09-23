using System.Composition.Hosting;
using System.Linq.Expressions;
using System.Reflection;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.TestSupport;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
            Assert.IsTrue(result.Contains("C:", StringComparison.OrdinalIgnoreCase), $"expected C: line in '{result}'");
            Assert.IsTrue(result.Contains("->null"), $"expected '->null' suffix in '{result}'");
        }

        [TestMethod]
        public void T2_KeyAddedInNew_ContainsAddedLine()
        {
            var service = NewService();

            var oldE = new TestAuditEntity { A = "1" };
            var newE = new TestAuditEntity { A = "1", D = "new-field" };

            var result = InvokeGetChangeDetail(service, oldE, newE);

            Assert.IsTrue(result.Contains("D:", StringComparison.OrdinalIgnoreCase), $"expected D: line in '{result}'");
            Assert.IsTrue(result.StartsWith("D:null->", StringComparison.OrdinalIgnoreCase)
                || result.Contains(",D:null->", StringComparison.OrdinalIgnoreCase),
                $"expected 'D:null->...' in '{result}'");
            Assert.IsFalse(result.Contains("A:", StringComparison.OrdinalIgnoreCase), $"A should be skipped (unchanged) in '{result}'");
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

            Assert.IsTrue(result.Contains("A:\"before\"->\"after\"", StringComparison.OrdinalIgnoreCase),
                $"expected A:\"before\"->\"after\" in '{result}'");
        }

        [TestMethod]
        public void T3c_MultipleChanges_OnlyChangedFieldsAppear()
        {
            var service = NewService();

            var oldE = new TestAuditEntity { A = "1", B = "2", C = "3" };
            var newE = new TestAuditEntity { A = "1", B = "20", C = "30" };

            var result = InvokeGetChangeDetail(service, oldE, newE);

            Assert.IsTrue(result.Contains("B:\"2\"->\"20\"", StringComparison.OrdinalIgnoreCase), $"expected B line in '{result}'");
            Assert.IsTrue(result.Contains("C:\"3\"->\"30\"", StringComparison.OrdinalIgnoreCase), $"expected C line in '{result}'");
            Assert.IsFalse(result.Contains("A:", StringComparison.OrdinalIgnoreCase), $"A unchanged should be skipped in '{result}'");
        }

        private static TestEntityService<TestAuditEntity> NewService()
            => new TestEntityService<TestAuditEntity>(BuildResolver());

        private static string InvokeGetChangeDetail(TestEntityService<TestAuditEntity> service, TestAuditEntity oldT, TestAuditEntity newT)
        {
            // GetChangeDetail 是 ServiceCore<T> 上的私有方法，使用 closed generic 类型的 MethodInfo。
            // 注意：该方法为 static（内部已不依赖任何实例状态），
            // 因此这里同时带上 Instance 与 Static 标志，两种形态都能取到。
            var method = typeof(ServiceCore<TestAuditEntity>).GetMethod(
                    "GetChangeDetail", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
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

        private sealed class TestEntityService<T> : EntityServiceBaseCore<T> where T : class, IEntityKey
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
            public Task<bool> TrySetStringAsync(string key, string value, TimeSpan ttl, CacheScope scope, string scopeId = "") => Task.FromResult(true);
        }

        // 最小桩：仅让 ServiceCore 构造成功；任何实际方法被调用都抛异常（GetChangeDetail 不触发）。
        // 复用共享的 StubRepository<T>（见 StubRepository.cs）。
    }
}
