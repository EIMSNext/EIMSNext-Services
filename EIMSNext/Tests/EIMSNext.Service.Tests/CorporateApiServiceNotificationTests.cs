using System.Composition.Hosting;
using System.Linq.Expressions;
using EIMSNext.ApiService;
using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.TestSupport;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace EIMSNext.Service.Tests;

[TestClass]
public class CorporateApiServiceNotificationTests
{
    [TestMethod]
    public async Task AddAsync_WithServiceContracts_PublishesEscapedEmailToDistinctRecipients()
    {
        var publisher = new RecordingPublisher();
        var resolver = CreateResolver("ops@example.com; OPS@example.com, audit@example.com", publisher);
        var service = new CorporateApiService(resolver);
        var corporate = new Corporate
        {
            Id = "corp-mail-test",
            Name = "Contoso <script>",
            Code = "C-001",
            Description = "<b>description</b>"
        };

        await service.AddAsync(corporate);

        Assert.AreEqual(1, publisher.Messages.Count);
        var message = (EmailNotifyTaskArgs)publisher.Messages.Single();
        Assert.AreEqual(EmailTaskType.PlatWork, message.TaskType);
        Assert.AreEqual(corporate.Id, message.CorpId);
        Assert.AreEqual(corporate.Id, message.NotifyId);
        CollectionAssert.AreEquivalent(
            new List<string?> { "ops@example.com", "audit@example.com" },
            message.Receivers.Select(x => x.Email).ToList());
        Assert.IsTrue(message.Receivers.All(x => x.EmpName == "ServiceContracts"));
        StringAssert.Contains(message.Detail, "Contoso &lt;script&gt;");
        StringAssert.Contains(message.Detail, "&lt;b&gt;description&lt;/b&gt;");
        Assert.IsFalse(message.Detail.Contains("<script>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AddAsync_WithoutServiceContracts_DoesNotPublishEmail()
    {
        var publisher = new RecordingPublisher();
        var service = new CorporateApiService(CreateResolver(string.Empty, publisher));

        await service.AddAsync(new Corporate { Id = "corp-no-mail", Name = "No Mail", Code = "C-002" });

        Assert.AreEqual(0, publisher.Messages.Count);
    }

    private static TestResolver CreateResolver(string serviceContracts, RecordingPublisher publisher)
    {
        var owner = new User
        {
            Id = "owner-1",
            Name = "Owner <unsafe>",
            Email = "owner@example.com",
            Phone = "13800138000",
            CreateTime = 1_735_689_600_000
        };
        var services = new Dictionary<Type, object>
        {
            [typeof(ICorporateService)] = new RecordingCorporateService(),
            [typeof(IService<User>)] = new RecordingEntityService<User>(owner),
            [typeof(IOutboxPublisher)] = publisher,
            [typeof(IConfiguration)] = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["ServiceContracts"] = serviceContracts }).Build(),
            [typeof(ICacheClient)] = new NullCacheClient(),
            [typeof(IMemoryCache)] = new MemoryCache(new MemoryCacheOptions()),
            [typeof(IIdentityContext)] = new TestIdentityContext(),
            [typeof(IServiceContext)] = new TestServiceContext { UserId = owner.Id, User = owner }
        };
        return new TestResolver(services);
    }

    private sealed class RecordingPublisher : IOutboxPublisher
    {
        public List<object> Messages { get; } = [];

        public Task EnqueueAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync<TMessage>(string idempotencyKey, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : class, IOutboxMessage
        {
            Messages.Add(message);
            return Task.CompletedTask;
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

    private sealed class TestIdentityContext : IIdentityContext
    {
        public string CurrentUserID => string.Empty;
        public IUser? CurrentUser => null;
        public IEmployee? CurrentEmployee => null;
        public IdentityType IdentityType => IdentityType.PlatAdmin;
        public AccessControlLevel AccessControlLevel { get; set; }
        public string CurrentCorpId => string.Empty;
        public string CurrentDashboardId => string.Empty;
        public string AccessToken => string.Empty;
        public PublicScope PublicScope => default;
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
        public IScopeCache ScopeCache => throw new NotSupportedException();
        public T? UserAs<T>() where T : class, IUser => User as T;
    }

    private sealed class NullCacheClient : ICacheClient
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

    /// <summary>
    /// 只读的内存实体服务。
    /// </summary>
    private class RecordingEntityService<T>(T? item = null) : StubEntityService<T>
        where T : class, EIMSNext.Core.Abstractions.IEntityKey
    {
        public override Task<T?> GetAsync(string id)
            => Task.FromResult(item?.Id == id ? item : null);
    }

    private sealed class RecordingCorporateService : RecordingEntityService<Corporate>, ICorporateService
    {
    }
}
