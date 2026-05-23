using System.Composition.Hosting;
using System.Linq.Expressions;

using EIMSNext.Auth.Entities;
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
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using MongoDB.Driver;
using MongoDB.Driver.Search;

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
        private InMemoryRepository<User> _userRepo = null!;
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
            _userRepo = new InMemoryRepository<User>();
            _deptRepo = new InMemoryRepository<Department>();
            _auditLogRepo = new InMemoryRepository<AuditLog>();

            var user = new User
            {
                Id = UserId,
                Name = UserName,
                Phone = "13800138000",
                Email = "test@test.com",
                Crops = []
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
                [typeof(IRepository<User>)] = _userRepo,
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
            var corp = new Corporate { Id = id, Name = name, Code = string.IsNullOrEmpty(code) ? id : code };
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
                DepartmentId = deptId,
                Code = "E001",
                EmpName = userName,
                UserBound = true,
                Status = EmployeeStatus.PendingReview,
                UserId = userId,
                UserName = userName
            };
            _empRepo.EnsureId(emp);
            await _empRepo.InsertAsync(emp);
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
                Name = UserName,
                Crops = []
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
                Name = "第二用户",
                Crops = []
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
            public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();

            public T? Get(string id, IClientSessionHandle? session = null) => _items.TryGetValue(id, out var entity) ? entity : null;
            public Task<T?> GetAsync(string id, IClientSessionHandle? session = null) => Task.FromResult(Get(id, session));

            public long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Queryable.LongCount(filter);
            public long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Task.FromResult(Count(filter, session, options));
            public Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();

            public void Insert(T entity, IClientSessionHandle? session = null) => _items[EnsureId(entity).Id] = entity;

            public void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null)
            {
                foreach (var entity in entities) Insert(entity, session);
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

            public DeleteResult Delete(string id, IClientSessionHandle? session = null)
            {
                _items.Remove(id);
                return null!;
            }

            public DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();

            public Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null)
            {
                Delete(id, session);
                return Task.FromResult<DeleteResult>(null!);
            }

            public Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null) => throw new NotSupportedException();

            public IEnumerable<T> EnsureId(IEnumerable<T> entities)
            {
                foreach (var entity in entities) yield return EnsureId(entity);
            }

            public T EnsureId(T entity)
            {
                if (string.IsNullOrWhiteSpace(entity.Id)) entity.Id = NewId();
                return entity;
            }

            public string NewId() => $"{typeof(T).Name}-{++_nextId}";
        }
    }
}
