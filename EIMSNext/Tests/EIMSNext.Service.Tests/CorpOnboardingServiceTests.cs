using System.Composition.Hosting;
using System.Linq.Expressions;

using EIMSNext.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;

using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using EIMSNext.TestSupport;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class CorpOnboardingServiceTests
    {
        private const string CorpId = "corp-test";
        private const string CorpName = "测试企业";
        private const string UserId = "user-test";
        private const string UserName = "测试用户";
        private const string EmpId = "emp-test";

        private InMemoryRepository<CorpOnboardingRequest> _requestRepo = null!;
        private InMemoryRepository<Corporate> _corpRepo = null!;
        private InMemoryRepository<Employee> _empRepo = null!;
        private InMemoryRepository<EmployeeDepartment> _empDeptRepo = null!;
        private InMemoryRepository<User> _userRepo = null!;
        private InMemoryRepository<UserCorp> _userCorpRepo = null!;
        private InMemoryRepository<Department> _deptRepo = null!;
        private InMemoryRepository<AuditLog> _auditLogRepo = null!;
        private FakeServiceContext _serviceContext = null!;
        private CorpOnboardingService _service = null!;

        [TestInitialize]
        public void Init()
        {
            _requestRepo = new InMemoryRepository<CorpOnboardingRequest>();
            _corpRepo = new InMemoryRepository<Corporate>();
            _empRepo = new InMemoryRepository<Employee>();
            _empDeptRepo = new InMemoryRepository<EmployeeDepartment>();
            _userRepo = new InMemoryRepository<User>();
            _userCorpRepo = new InMemoryRepository<UserCorp>();
            _deptRepo = new InMemoryRepository<Department>();
            _auditLogRepo = new InMemoryRepository<AuditLog>();

            var user = new User
            {
                Id = UserId,
                Name = UserName,
                Phone = "13800138000",
                Email = "test@test.com"
            };

            _serviceContext = new FakeServiceContext
            {
                UserId = UserId,
                User = user,
                CorpId = CorpId,
                Operator = new Operator(EmpId, "E001", UserName)
            };

            var services = new Dictionary<Type, object>
            {
                [typeof(IRepository<CorpOnboardingRequest>)] = _requestRepo,
                [typeof(IRepository<Corporate>)] = _corpRepo,
                [typeof(IRepository<Employee>)] = _empRepo,
                [typeof(IRepository<EmployeeDepartment>)] = _empDeptRepo,
                [typeof(IRepository<User>)] = _userRepo,
                [typeof(IRepository<UserCorp>)] = _userCorpRepo,
                [typeof(IRepository<Department>)] = _deptRepo,
                [typeof(IRepository<AuditLog>)] = _auditLogRepo,
                [typeof(ICacheClient)] = new FakeCacheClient(),
                [typeof(IScopeCache)] = new FakeScopeCache(),
                [typeof(IServiceContext)] = _serviceContext,
                [typeof(ILogger<CorpOnboardingRequest>)] = new FakeLogger<CorpOnboardingRequest>()
            };

            var resolver = new TestResolver(services);
            _service = new CorpOnboardingService(resolver);
        }

        [TestMethod]
        public async Task ApplyJoinCorporateAsync_Valid_CreatesEmployeeAndRequestFromCurrentUser()
        {
            var corp = await SeedCorporateAsync();
            var user = _serviceContext.UserAs<User>()!;
            await SeedDefaultDepartmentAsync(corp.Id);

            await _service.ApplyJoinCorporateAsync(corp.Id, user);

            var request = _requestRepo.Queryable.Single(x => x.TargetCorpId == corp.Id);
            Assert.AreEqual(UserId, request.UserId);
            Assert.AreEqual(UserName, request.ApplicantName);
            Assert.AreEqual("13800138000", request.Phone);
            Assert.AreEqual("test@test.com", request.Email);

            var employee = _empRepo.Get(request.EmployeeId);
            Assert.IsNotNull(employee);
            Assert.AreEqual(UserName, employee.EmpName);
            Assert.AreEqual("13800138000", employee.WorkPhone);
            Assert.AreEqual("test@test.com", employee.WorkEmail);
            Assert.IsTrue(employee.UserBound);
            Assert.AreEqual(EmployeeStatus.PendingReview, employee.Status);
            Assert.IsTrue(_empDeptRepo.Queryable.Any(x => x.EmployeeId == employee.Id));
        }

        [TestMethod]
        public async Task ApplyJoinCorporateAsync_DuplicateRequest_Throws()
        {
            var corp = await SeedCorporateAsync();
            var user = _serviceContext.UserAs<User>()!;
            await SeedDefaultDepartmentAsync(corp.Id);

            await _service.ApplyJoinCorporateAsync(corp.Id, user);

            await AssertThrowsAsync<ConflictException>(() => _service.ApplyJoinCorporateAsync(corp.Id, user));
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

        private async Task<Corporate> SeedCorporateAsync(string id = CorpId, string name = CorpName, string code = "")
        {
            var corp = new Corporate { Id = id, Name = name };
            _corpRepo.EnsureId(corp);
            await _corpRepo.InsertAsync(corp);
            return corp;
        }

        private async Task<Department> SeedDefaultDepartmentAsync(string corpId)
        {
            var dept = new Department
            {
                CorpId = corpId,
                Code = "0",
                Name = "默认部门",
                HeriarchyId = "|root|",
                HeriarchyName = "默认部门"
            };
            _deptRepo.EnsureId(dept);
            await _deptRepo.InsertAsync(dept);
            return dept;
        }

        private async Task<Employee> SeedEmployeeAsync(string corpId, string deptId, string userId = UserId, string userName = UserName)
        {
            var emp = new Employee
            {
                CorpId = corpId,
                Code = "E001",
                EmpName = userName,
                UserBound = true,
                Status = EmployeeStatus.PendingReview,
                UserId = userId,
                UserName = userName
            };
            _empRepo.EnsureId(emp);
            await _empRepo.InsertAsync(emp);
            var relation = new EmployeeDepartment
            {
                CorpId = corpId,
                EmployeeId = emp.Id,
                DepartmentId = deptId
            };
            _empDeptRepo.EnsureId(relation);
            await _empDeptRepo.InsertAsync(relation);
            return emp;
        }

        private async Task<(Corporate corp, CorpOnboardingRequest request, Employee employee)> SeedPendingRequestAsync()
        {
            var corp = await SeedCorporateAsync();
            var dept = await SeedDefaultDepartmentAsync(corp.Id);
            var emp = await SeedEmployeeAsync(corp.Id, dept.Id);

            var user = new User
            {
                Id = UserId,
                Name = UserName
            };
            _userRepo.EnsureId(user);
            await _userRepo.InsertAsync(user);

            var request = new CorpOnboardingRequest
            {
                UserId = UserId,
                UserName = UserName,
                TargetCorpId = corp.Id,
                TargetCorpName = corp.Name,
                ApplicantName = UserName,
                EmployeeId = emp.Id,
                SourceType = CorpOnboardingSourceType.UserApply,
            };
            _requestRepo.EnsureId(request);
            await _requestRepo.InsertAsync(request);

            return (corp, request, emp);
        }

        private async Task<(CorpOnboardingRequest request, Employee employee)> SeedSecondPendingRequestAsync(string corpId)
        {
            var dept = _deptRepo.Queryable.First(x => x.CorpId == corpId);
            var user = new User
            {
                Id = "user-second",
                Name = "第二用户"
            };
            _userRepo.EnsureId(user);
            await _userRepo.InsertAsync(user);

            var employee = await SeedEmployeeAsync(corpId, dept.Id, user.Id, user.Name);
            var request = new CorpOnboardingRequest
            {
                UserId = user.Id,
                UserName = user.Name,
                TargetCorpId = corpId,
                TargetCorpName = CorpName,
                ApplicantName = user.Name,
                EmployeeId = employee.Id,
                SourceType = CorpOnboardingSourceType.UserApply,
            };
            _requestRepo.EnsureId(request);
            await _requestRepo.InsertAsync(request);

            return (request, employee);
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
            public string AccessToken { get; set; } = "";
            public string CorpId { get; set; } = "";
            public Operator? Operator { get; set; }
            public string UserId { get; set; } = "";
            public IUser? User { get; set; }
            public IEmployee? Employee { get; set; }
            public string? ClientIp { get; set; }
            public DataAction Action { get; set; }
            public IScopeCache ScopeCache => throw new NotSupportedException();

            public T? UserAs<T>() where T : class, IUser => User as T;
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

        private sealed class FakeScopeCache : IScopeCache
        {
            public IEnumerable<T> GetAll<T>(DataVersion version = DataVersion.Temp) where T : class => [];
            public T? Get<T>(string key, DataVersion version = DataVersion.Temp, Func<string, T?>? getter = null) where T : class => getter?.Invoke(key);
            public void Set<T>(string key, T value, DataVersion version = DataVersion.Temp) where T : class { }
            public void Remove<T>(string key, DataVersion version = DataVersion.Temp) where T : class { }
            public bool Contains<T>(string key, DataVersion version = DataVersion.Temp) where T : class => false;
        }

        private sealed class FakeLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        /// <summary>
        /// <c>FilterDefinitionBuilder</c> / <c>UpdateDefinition</c> / <c>IClientSessionHandle</c> /
        /// </summary>
        private sealed class InMemoryRepository<T> : StubRepository<T> where T : class, IEntityKey
        {
            private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);
            private int _nextId;

            public override IQueryable<T> Queryable => _items.Values.AsQueryable();

            public override T? Get(string id) => _items.TryGetValue(id, out var entity) ? entity : null;

            public override Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(id));

            public override long Count(Expression<Func<T, bool>> predicate) => Queryable.LongCount(predicate);

            public override Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
                => Task.FromResult(Queryable.Any(predicate));

            public override void Insert(T entity) => _items[EnsureId(entity).Id] = entity;

            public override void Insert(IEnumerable<T> entities)
            {
                foreach (var entity in entities)
                {
                    Insert(entity);
                }
            }

            public override Task InsertAsync(T entity, CancellationToken cancellationToken = default)
            {
                Insert(entity);
                return Task.CompletedTask;
            }

            public override Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
            {
                Insert(entities);
                return Task.CompletedTask;
            }

            public override void Replace(T entity) => _items[EnsureId(entity).Id] = entity;

            public override Task ReplaceAsync(T entity, CancellationToken cancellationToken = default)
            {
                Replace(entity);
                return Task.CompletedTask;
            }

            public override int Delete(string id) => _items.Remove(id) ? 1 : 0;

            public override Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default)
                => Task.FromResult(Delete(id));

            public override T EnsureId(T entity)
            {
                if (string.IsNullOrWhiteSpace(entity.Id))
                {
                    entity.Id = NewId();
                }

                return entity;
            }

            public override string NewId() => $"{typeof(T).Name}-{++_nextId}";
        }
    }
}
