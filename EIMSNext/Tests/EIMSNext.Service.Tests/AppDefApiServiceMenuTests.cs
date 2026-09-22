using System.Composition.Hosting;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;
using EIMSNext.TestSupport;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class AppDefApiServiceMenuTests
    {
        private const string CorpId = "corp-app-menu";
        private const string AppId = "app-menu";

        private FakeAppDefService _appDefService = null!;
        private AppDefApiService _apiService = null!;

        [TestInitialize]
        public void Init()
        {
            _appDefService = new FakeAppDefService();
            _appDefService.Add(SeedApp());

            var identityContext = new FakeIdentityContext();
            var services = new Dictionary<Type, object>
            {
                [typeof(IAppDefService)] = _appDefService,
                [typeof(IService<AppDef>)] = _appDefService,
                [typeof(IService<TenantAdminGroup>)] = new FakeEntityService<TenantAdminGroup>(),
                [typeof(ITenantAdminGroupService)] = new FakeTenantAdminGroupService(),
                [typeof(IService<DashboardDef>)] = new FakeEntityService<DashboardDef>(),
                [typeof(IService<FormDef>)] = new FakeEntityService<FormDef>(),
                [typeof(IService<FormDataPermissionGroup>)] = new FakeEntityService<FormDataPermissionGroup>(),
                [typeof(IService<Department>)] = new FakeEntityService<Department>(),
                [typeof(IService<Employee>)] = new FakeEntityService<Employee>(),
                [typeof(IIdentityContext)] = identityContext,
                [typeof(IServiceContext)] = new FakeServiceContext(),
                [typeof(ICacheClient)] = new FakeCacheClient(),
                [typeof(IMemoryCache)] = new MemoryCache(new MemoryCacheOptions()),
            };

            var resolver = new TestResolver(services);
            services[typeof(TenantAccessEvaluator)] = new TenantAccessEvaluator(resolver);
            _apiService = new AppDefApiService(resolver);
        }

        [TestMethod]
        public async Task SaveMenus_ReordersOnlyAndPreservesExistingMetadata()
        {
            var result = await _apiService.SaveMenus(new SaveAppMenusRequest
            {
                AppId = AppId,
                AppMenus =
                [
                    new AppMenu { MenuId = "dashboard-1", Title = "changed", Icon = "changed", IconColor = "changed", MenuType = FormType.Dashboard },
                    new AppMenu
                    {
                        MenuId = "group-1",
                        Title = "changed",
                        MenuType = FormType.Group,
                        SubMenus =
                        [
                            new AppMenu { MenuId = "form-1", Title = "changed", Icon = "changed", IconColor = "changed", MenuType = FormType.Form },
                        ],
                    },
                ],
            });

            Assert.AreEqual("dashboard-1", result.AppMenus[0].MenuId);
            Assert.AreEqual("Dashboard", result.AppMenus[0].Title);
            Assert.AreEqual("dash-icon", result.AppMenus[0].Icon);
            Assert.AreEqual("#222222", result.AppMenus[0].IconColor);
            Assert.AreEqual(100, result.AppMenus[0].SortIndex);
            Assert.IsFalse(result.AppMenus[0].Editable);
            Assert.IsFalse(result.AppMenus[0].Deletable);
            Assert.AreEqual("custom/dashboard/list", result.AppMenus[0].ListComponent);

            var group = result.AppMenus[1];
            Assert.AreEqual("group-1", group.MenuId);
            Assert.AreEqual("Group", group.Title);
            Assert.AreEqual(200, group.SortIndex);
            Assert.AreEqual(1, group.SubMenus?.Count);
            Assert.AreEqual("form-1", group.SubMenus![0].MenuId);
            Assert.AreEqual("Form", group.SubMenus[0].Title);
            Assert.AreEqual("form-icon", group.SubMenus[0].Icon);
            Assert.AreEqual("#111111", group.SubMenus[0].IconColor);
            Assert.AreEqual(100, group.SubMenus[0].SortIndex);
        }

        [TestMethod]
        public async Task SaveMenus_RejectsMissingUnknownDuplicateAndTypeChangedMenus()
        {
            await AssertThrowsAsync<BadRequestException>(() => _apiService.SaveMenus(new SaveAppMenusRequest
            {
                AppId = AppId,
                AppMenus =
                [
                    Submitted("form-1", FormType.Form),
                    Submitted("group-1", FormType.Group),
                ],
            }));

            await AssertThrowsAsync<BadRequestException>(() => _apiService.SaveMenus(new SaveAppMenusRequest
            {
                AppId = AppId,
                AppMenus =
                [
                    Submitted("form-1", FormType.Form),
                    Submitted("dashboard-1", FormType.Dashboard),
                    Submitted("group-1", FormType.Group),
                    Submitted("unknown", FormType.Form),
                ],
            }));

            await AssertThrowsAsync<BadRequestException>(() => _apiService.SaveMenus(new SaveAppMenusRequest
            {
                AppId = AppId,
                AppMenus =
                [
                    Submitted("form-1", FormType.Form),
                    Submitted("form-1", FormType.Form),
                    Submitted("dashboard-1", FormType.Dashboard),
                ],
            }));

            await AssertThrowsAsync<BadRequestException>(() => _apiService.SaveMenus(new SaveAppMenusRequest
            {
                AppId = AppId,
                AppMenus =
                [
                    Submitted("form-1", FormType.Dashboard),
                    Submitted("dashboard-1", FormType.Dashboard),
                    Submitted("group-1", FormType.Group),
                ],
            }));
        }

        private static AppDef SeedApp()
        {
            return new AppDef
            {
                Id = AppId,
                CorpId = CorpId,
                Name = "App",
                AppMenus =
                [
                    new AppMenu { MenuId = "form-1", Title = "Form", Icon = "form-icon", IconColor = "#111111", MenuType = FormType.Form },
                    new AppMenu
                    {
                        MenuId = "dashboard-1",
                        Title = "Dashboard",
                        Icon = "dash-icon",
                        IconColor = "#222222",
                        MenuType = FormType.Dashboard,
                        Editable = false,
                        Deletable = false,
                        ListComponent = "custom/dashboard/list"
                    },
                    new AppMenu { MenuId = "group-1", Title = "Group", Icon = "group-icon", IconColor = "#333333", MenuType = FormType.Group, SubMenus = [] },
                ],
            };
        }

        private static AppMenu Submitted(string id, FormType type)
        {
            return new AppMenu { MenuId = id, Title = id, MenuType = type, SubMenus = type == FormType.Group ? [] : null };
        }

        private static async Task AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
        {
            try
            {
                await action();
                Assert.Fail($"Expected {typeof(TException).Name} but no exception was thrown");
            }
            catch (TException)
            {
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
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

        private sealed class FakeIdentityContext : IIdentityContext
        {
            public string CurrentUserID => "user";
            public IUser? CurrentUser => null;
            public IEmployee? CurrentEmployee => null;
            public IdentityType IdentityType => IdentityType.CorpAdmin;
            public PublicScope PublicScope => PublicScope.None;
            public AccessControlLevel AccessControlLevel { get; set; } = AccessControlLevel.Allow;
            public string CurrentCorpId => CorpId;
            public string CurrentDashboardId => string.Empty;
            public string AccessToken => string.Empty;
        }

        private sealed class FakeServiceContext : IServiceContext
        {
            public string AccessToken { get; set; } = string.Empty;
            public string CorpId { get; set; } = AppDefApiServiceMenuTests.CorpId;
            public Operator? Operator { get; set; }
            public string UserId { get; set; } = string.Empty;
            public IUser? User { get; set; }
            public IEmployee? Employee { get; set; }
            public string? ClientIp { get; set; }
            public DataAction Action { get; set; }
            public IScopeCache ScopeCache => throw new NotSupportedException();
            public T? UserAs<T>() where T : class, IUser => User as T;
        }

        private sealed class FakeAppDefService : FakeEntityService<AppDef>, IAppDefService
        {
        }

        private sealed class FakeTenantAdminGroupService : FakeEntityService<TenantAdminGroup>, ITenantAdminGroupService
        {
        }

        /// <summary>
        /// 内存字典版实体服务。
        /// <para>
        /// 迁移说明：原实现逐个实现了 Mongo 时代的 <c>IService&lt;T&gt;</c> 成员
        /// （<c>IMongoCollection</c> / <c>IFindFluent</c> / <c>IAsyncCursor</c> /
        /// <c>ReplaceOneResult</c>）、以及全部返回 <c>object</c> 的删除方法。
        /// 迁移后 <see cref="IService{T}"/> 已是 EF Core 形态，返回值统一为
        /// <see cref="IQueryable{T}"/> 与 <c>int</c> 受影响行数；本测试只依赖
        /// 「按 Id 存取」这一语义，因此直接继承共享桩并覆写对应成员。
        /// </para>
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        private class FakeEntityService<T> : StubEntityService<T>
            where T : class, IEntityKey
        {
            private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);

            public override T? Get(string id) => _items.GetValueOrDefault(id);

            public override IQueryable<T> All() => _items.Values.AsQueryable();

            public override void Add(T entity) => _items[entity.Id] = entity;

            public override void Add(IEnumerable<T> entities)
            {
                foreach (var entity in entities) Add(entity);
            }

            public override int Replace(T entity)
            {
                _items[entity.Id] = entity;
                return 1;
            }

            public override int Delete(string id) => _items.Remove(id) ? 1 : 0;

            public override int Delete(IEnumerable<string> ids)
            {
                var affected = 0;
                foreach (var id in ids)
                {
                    if (_items.Remove(id)) affected++;
                }

                return affected;
            }
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
    }
}
