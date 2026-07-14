using System.Composition.Hosting;
using System.Linq.Expressions;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class EmployeeDepartmentRelationshipTests
    {
        private const string CorpId = "corp-employee-dept";

        private InMemoryRepository<Employee> _employeeRepo = null!;
        private InMemoryRepository<EmployeeDepartment> _employeeDepartmentRepo = null!;
        private InMemoryRepository<Department> _departmentRepo = null!;
        private InMemoryRepository<AuthGroup> _authGroupRepo = null!;
        private InMemoryRepository<AdminGroup> _adminGroupRepo = null!;
        private FakeIdentityContext _identityContext = null!;
        private EmployeeApiService _employeeApiService = null!;
        private TestableDepartmentService _departmentService = null!;
        private AdminPermissionEvaluator _permissionEvaluator = null!;

        [TestInitialize]
        public void Init()
        {
            _employeeRepo = new InMemoryRepository<Employee>();
            _employeeDepartmentRepo = new InMemoryRepository<EmployeeDepartment>();
            _departmentRepo = new InMemoryRepository<Department>();
            _authGroupRepo = new InMemoryRepository<AuthGroup>();
            _adminGroupRepo = new InMemoryRepository<AdminGroup>();

            var corporateRepo = new InMemoryRepository<Corporate>();
            corporateRepo.Insert(new Corporate { Id = CorpId, Name = "Test Corp", Platform = PlatformType.Public });

            var serviceContext = new FakeServiceContext
            {
                CorpId = CorpId,
                Operator = new Operator("op", "OP", "Operator")
            };
            _identityContext = new FakeIdentityContext(CorpId);

            var services = new Dictionary<Type, object>
            {
                [typeof(IRepository<Employee>)] = _employeeRepo,
                [typeof(IRepository<EmployeeDepartment>)] = _employeeDepartmentRepo,
                [typeof(IRepository<Department>)] = _departmentRepo,
                [typeof(IRepository<AuthGroup>)] = _authGroupRepo,
                [typeof(IRepository<AdminGroup>)] = _adminGroupRepo,
                [typeof(IRepository<Corporate>)] = corporateRepo,
                [typeof(IRepository<AuditLog>)] = new InMemoryRepository<AuditLog>(),
                [typeof(IEmployeeService)] = new FakeEmployeeService(_employeeRepo),
                [typeof(IAuthGroupService)] = new FakeAuthGroupService(_authGroupRepo),
                [typeof(IAdminGroupService)] = new FakeAdminGroupService(_adminGroupRepo),
                [typeof(IService<Corporate>)] = new FakeEntityService<Corporate>(corporateRepo),
                [typeof(IService<User>)] = new FakeEntityService<User>(new InMemoryRepository<User>()),
                [typeof(IService<AuthGroup>)] = new FakeEntityService<AuthGroup>(_authGroupRepo),
                [typeof(IService<AdminGroup>)] = new FakeEntityService<AdminGroup>(_adminGroupRepo),
                [typeof(IService<Department>)] = new FakeEntityService<Department>(_departmentRepo),
                [typeof(IService<Employee>)] = new FakeEntityService<Employee>(_employeeRepo),
                [typeof(ICacheClient)] = new FakeCacheClient(),
                [typeof(IScopeCache)] = new FakeScopeCache(),
                [typeof(IMemoryCache)] = new MemoryCache(new MemoryCacheOptions()),
                [typeof(IIdentityContext)] = _identityContext,
                [typeof(IServiceContext)] = serviceContext,
                [typeof(ILogger<Employee>)] = new FakeLogger<Employee>(),
                [typeof(ILogger<Department>)] = new FakeLogger<Department>(),
                [typeof(ILogger<Corporate>)] = new FakeLogger<Corporate>()
            };

            var resolver = new TestResolver(services);
            _permissionEvaluator = new AdminPermissionEvaluator(resolver);
            services[typeof(AdminPermissionEvaluator)] = _permissionEvaluator;
            _employeeApiService = new EmployeeApiService(resolver);
            _departmentService = new TestableDepartmentService(resolver);
        }

        [TestMethod]
        public async Task AddEmployee_WithoutDepartment_Throws()
        {
            var employee = new Employee { CorpId = CorpId, Code = "E001", EmpName = "No Department" };

            await AssertThrowsAsync<BadRequestException>(() => _employeeApiService.AddAsync(employee, []));
        }

        [TestMethod]
        public async Task AddEmployee_WithMultipleDepartments_SavesRelationsAndReturnsDepartmentRefs()
        {
            var deptA = SeedDepartment("dept-a", "研发部");
            var deptB = SeedDepartment("dept-b", "运营部");
            var employee = new Employee { CorpId = CorpId, Code = "E002", EmpName = "Multi Department" };

            await _employeeApiService.AddAsync(employee,
            [
                new EmployeeDepartmentRequest { DepartmentId = deptA.Id, IsManager = true, SortValue = 1 },
                new EmployeeDepartmentRequest { DepartmentId = deptB.Id, SortValue = 2 }
            ]);

            var relations = _employeeDepartmentRepo.Queryable.Where(x => x.EmployeeId == employee.Id).OrderBy(x => x.SortValue).ToList();
            Assert.AreEqual(2, relations.Count);
            Assert.AreEqual(deptA.Id, relations[0].DepartmentId);
            Assert.IsTrue(relations[0].IsManager);
            Assert.AreEqual(deptB.Id, relations[1].DepartmentId);

            Assert.AreEqual(2, employee.Depts.Count);
            CollectionAssert.AreEqual(new[] { "研发部", "运营部" }, employee.Depts.Select(x => x.DeptName).ToArray());
        }

        [TestMethod]
        public async Task DeleteDepartment_WithEmployeeInChildDepartment_Throws()
        {
            var parent = SeedDepartment("dept-parent", "总部");
            var child = SeedDepartment("dept-child", "研发部", parent.Id);
            var employee = new Employee { Id = "emp-001", CorpId = CorpId, Code = "E003", EmpName = "Child Employee" };
            _employeeRepo.Insert(employee);
            _employeeDepartmentRepo.Insert(new EmployeeDepartment
            {
                CorpId = CorpId,
                EmployeeId = employee.Id,
                DepartmentId = child.Id
            });

            await AssertThrowsAsync<BadRequestException>(() =>
                _departmentService.InvokeBeforeDeleteAsync(Builders<Department>.Filter.Eq(x => x.Id, parent.Id)));
        }

        [TestMethod]
        public void FilterByDepartment_WithCascadedParent_ReturnsChildDepartmentEmployees()
        {
            var parent = SeedDepartment("dept-filter-parent", "Parent");
            var child = SeedDepartment("dept-filter-child", "Child", parent.Id);
            var sibling = SeedDepartment("dept-filter-sibling", "Sibling");
            var childEmployee = new EmployeeViewModel { Id = "emp-child", CorpId = CorpId, Code = "E004", EmpName = "Child Employee" };
            var siblingEmployee = new EmployeeViewModel { Id = "emp-sibling", CorpId = CorpId, Code = "E005", EmpName = "Sibling Employee" };
            _employeeDepartmentRepo.Insert(new EmployeeDepartment { CorpId = CorpId, EmployeeId = childEmployee.Id, DepartmentId = child.Id });
            _employeeDepartmentRepo.Insert(new EmployeeDepartment { CorpId = CorpId, EmployeeId = siblingEmployee.Id, DepartmentId = sibling.Id });

            var employees = new[] { childEmployee, siblingEmployee }.AsQueryable();

            var directResult = _employeeApiService.FilterByDepartment(employees, parent.Id, false).Select(x => x.Id).ToList();
            Assert.AreEqual(0, directResult.Count);

            var cascadedResult = _employeeApiService.FilterByDepartment(employees, parent.Id, true).Select(x => x.Id).ToList();
            CollectionAssert.AreEqual(new[] { childEmployee.Id }, cascadedResult);
        }

        [TestMethod]
        public void GetAncestorDepartmentIds_ReturnsAncestorsAndCurrentDepartment()
        {
            var root = SeedDepartment("dept-root", "Root");
            var child = SeedDepartment("dept-ancestor-child", "Child", root.Id);
            var grandChild = SeedDepartment("dept-ancestor-grand-child", "Grand Child", child.Id);
            var sibling = SeedDepartment("dept-ancestor-sibling", "Sibling", root.Id);

            var result = _employeeApiService.GetAncestorDepartmentIds([grandChild.Id]);

            CollectionAssert.Contains(result, root.Id);
            CollectionAssert.Contains(result, child.Id);
            CollectionAssert.Contains(result, grandChild.Id);
            CollectionAssert.DoesNotContain(result, sibling.Id);
        }

        [TestMethod]
        public void AuthGroup_WithCascadedParentDepartment_ReturnsChildDepartmentEmployee()
        {
            var parent = SeedDepartment("dept-auth-parent", "总部");
            var child = SeedDepartment("dept-auth-child", "研发部", parent.Id);
            var employee = SeedEmployee("emp-auth-child", "Child Employee", child.Id);
            _identityContext.IdentityTypeValue = IdentityType.Employee;
            _identityContext.CurrentEmployeeValue = employee;

            _authGroupRepo.Insert(new AuthGroup
            {
                Id = "auth-parent-cascade",
                CorpId = CorpId,
                AppId = "app-001",
                FormId = "form-001",
                Members =
                [
                    new Member
                    {
                        Id = parent.Id,
                        Label = parent.Name,
                        Type = MemberType.Department,
                        CascadedDept = true
                    }
                ]
            });

            var result = _permissionEvaluator.GetUsageFormIdsForCurrentEmployee("app-001");

            CollectionAssert.AreEqual(new[] { "form-001" }, result);
        }

        [TestMethod]
        public async Task AddEmployee_NormalAdminWithParentManageScope_AllowsChildDepartment()
        {
            var parent = SeedDepartment("dept-manage-parent", "总部");
            var child = SeedDepartment("dept-manage-child", "研发部", parent.Id);
            var admin = SeedEmployee("emp-admin", "Admin", parent.Id);
            _identityContext.IdentityTypeValue = IdentityType.AppAdmin;
            _identityContext.CurrentEmployeeValue = admin;
            _adminGroupRepo.Insert(new AdminGroup
            {
                Id = "admin-group",
                CorpId = CorpId,
                Type = AdminGroupType.Normal,
                EmployeeIds = [admin.Id],
                ContactDepartmentPermission = PermissionLevel.Manage,
                ContactDepartmentScopeMode = ScopeMode.Partial,
                ContactDepartmentIds = [parent.Id]
            });
            var employee = new Employee { CorpId = CorpId, Code = "E006", EmpName = "Child New Employee" };

            await _employeeApiService.AddAsync(employee,
            [
                new EmployeeDepartmentRequest { DepartmentId = child.Id }
            ]);

            Assert.IsTrue(_employeeDepartmentRepo.Queryable.Any(x => x.EmployeeId == employee.Id && x.DepartmentId == child.Id));
        }

        [TestMethod]
        public async Task AddEmployee_NormalAdminOutsideManageScope_Throws()
        {
            var allowed = SeedDepartment("dept-allowed", "允许部门");
            var denied = SeedDepartment("dept-denied", "禁止部门");
            var admin = SeedEmployee("emp-admin-denied", "Admin", allowed.Id);
            _identityContext.IdentityTypeValue = IdentityType.AppAdmin;
            _identityContext.CurrentEmployeeValue = admin;
            _adminGroupRepo.Insert(new AdminGroup
            {
                Id = "admin-group-denied",
                CorpId = CorpId,
                Type = AdminGroupType.Normal,
                EmployeeIds = [admin.Id],
                ContactDepartmentPermission = PermissionLevel.Manage,
                ContactDepartmentScopeMode = ScopeMode.Partial,
                ContactDepartmentIds = [allowed.Id]
            });
            var employee = new Employee { CorpId = CorpId, Code = "E007", EmpName = "Denied Employee" };

            await AssertThrowsAsync<ForbiddenException>(() => _employeeApiService.AddAsync(employee,
            [
                new EmployeeDepartmentRequest { DepartmentId = denied.Id }
            ]));
        }

        [TestMethod]
        public async Task ReplaceDepartment_RebuildsDescendantHierarchy()
        {
            var root = SeedDepartment("dept-replace-root", "Root");
            var parent = SeedDepartment("dept-replace-parent", "Parent", root.Id);
            var child = SeedDepartment("dept-replace-child", "Child", parent.Id);

            parent.Name = "New Parent";
            await _departmentService.InvokeReplaceAsync(parent);

            var updatedParent = _departmentRepo.Get(parent.Id)!;
            var updatedChild = _departmentRepo.Get(child.Id)!;
            Assert.AreEqual("New Parent/Root", updatedParent.HeriarchyName);
            Assert.AreEqual($"{root.HeriarchyId}{parent.Id}|{child.Id}|", updatedChild.HeriarchyId);
            Assert.AreEqual("Child/New Parent/Root", updatedChild.HeriarchyName);
        }

        private Department SeedDepartment(string id, string name, string? parentId = null)
        {
            var parent = string.IsNullOrWhiteSpace(parentId) ? null : _departmentRepo.Get(parentId);
            var department = new Department
            {
                Id = id,
                CorpId = CorpId,
                Code = id,
                Name = name,
                ParentId = parentId ?? string.Empty,
                HeriarchyId = parent == null ? $"|{id}|" : $"{parent.HeriarchyId}{id}|",
                HeriarchyName = parent == null ? name : $"{name}/{parent.HeriarchyName}"
            };
            _departmentRepo.Insert(department);
            return department;
        }

        private Employee SeedEmployee(string id, string name, string departmentId)
        {
            var employee = new Employee
            {
                Id = id,
                CorpId = CorpId,
                Code = id,
                EmpName = name,
                Status = EmployeeStatus.Active
            };
            _employeeRepo.Insert(employee);
            _employeeDepartmentRepo.Insert(new EmployeeDepartment
            {
                CorpId = CorpId,
                EmployeeId = employee.Id,
                DepartmentId = departmentId
            });
            return employee;
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

        private sealed class TestableDepartmentService(IResolver resolver) : DepartmentService(resolver)
        {
            public Task InvokeBeforeDeleteAsync(FilterDefinition<Department> filter) => BeforeDelete(filter, null);

            public async Task InvokeReplaceAsync(Department department)
            {
                await BeforeReplace(department, null);
                Resolver.GetRepository<Department>().Replace(department);
                await AfterReplace(department, null);
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

        private sealed class FakeIdentityContext(string corpId) : IIdentityContext
        {
            public string CurrentUserID => "user-test";
            public IUser? CurrentUser => null;
            public IEmployee? CurrentEmployee => CurrentEmployeeValue;
            public Employee? CurrentEmployeeValue { get; set; }
            public IdentityType IdentityType => IdentityTypeValue;
            public IdentityType IdentityTypeValue { get; set; } = IdentityType.CorpAdmin;
            public PublicScope PublicScope => PublicScope.None;
            public AccessControlLevel AccessControlLevel { get; set; } = AccessControlLevel.Allow;
            public string CurrentCorpId => corpId;
            public string CurrentDashboardId => string.Empty;
            public string AccessToken => string.Empty;
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
            public IScopeCache ScopeCache => throw new NotSupportedException();

            public T? UserAs<T>() where T : class, IUser => User as T;
        }

        private sealed class FakeEmployeeService(InMemoryRepository<Employee> repository)
            : FakeEntityService<Employee>(repository), IEmployeeService
        {
            public Task<UpdateResult> AddToRoleAsync(Role role, IEnumerable<string> empIds) => throw new NotSupportedException();
            public Task<UpdateResult> RemoveFromRoleAsync(string roleId, IEnumerable<string> empIds) => throw new NotSupportedException();
            public Task ReviewJoinCorporateAsync(IEnumerable<string> employeeIds, bool approved, string corpId) => throw new NotSupportedException();
            public Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted) => throw new NotSupportedException();
        }

        private sealed class FakeAuthGroupService(InMemoryRepository<AuthGroup> repository)
            : FakeEntityService<AuthGroup>(repository), IAuthGroupService
        {
        }

        private sealed class FakeAdminGroupService(InMemoryRepository<AdminGroup> repository)
            : FakeEntityService<AdminGroup>(repository), IAdminGroupService
        {
        }

        private class FakeEntityService<T>(InMemoryRepository<T> repository) : IService<T> where T : class, IMongoEntity
        {
            public IMongoCollection<T> Collection => throw new NotSupportedException();

            public T? Get(string id) => repository.Get(id);
            public IQueryable<T> All() => repository.Queryable;
            public IQueryable<T> Query(Expression<Func<T, bool>> where) => repository.Queryable.Where(where);
            public IFindFluent<T, T> Find(DynamicFindOptions<T> options) => repository.Find(options);
            public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter) => repository.Find(filter);
            public long Count(DynamicFilter filter) => throw new NotSupportedException();
            public long Count(Expression<Func<T, bool>> filter) => repository.Queryable.LongCount(filter);
            public bool Exists(Expression<Func<T, bool>> where) => repository.Queryable.Any(where);
            public bool Exists(DynamicFilter where) => throw new NotSupportedException();
            public void Add(T entity) => repository.Insert(entity);
            public void Add(IEnumerable<T> entities) => repository.Insert(entities);
            public ReplaceOneResult Replace(T entity) => repository.Replace(entity);
            public object Delete(string id) => repository.Delete(id);
            public object Delete(IEnumerable<string> ids) => repository.Delete(ids);
            public object Delete(DynamicFilter filter) => throw new NotSupportedException();
            public Task<T?> GetAsync(string id) => repository.GetAsync(id);
            public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options) => repository.FindAsync(options);
            public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter) => repository.FindAsync(filter);
            public Task<long> CountAsync(DynamicFilter filter) => throw new NotSupportedException();
            public Task<long> CountAsync(Expression<Func<T, bool>> filter) => Task.FromResult(Count(filter));
            public Task<bool> ExistsAsync(Expression<Func<T, bool>> where) => Task.FromResult(Exists(where));
            public Task<bool> ExistsAsync(DynamicFilter where) => throw new NotSupportedException();
            public Task AddAsync(T entity) => repository.InsertAsync(entity);
            public Task AddAsync(IEnumerable<T> entities) => repository.InsertAsync(entities);
            public Task<ReplaceOneResult> ReplaceAsync(T entity) => repository.ReplaceAsync(entity);
            public Task<object> DeleteAsync(string id) => Task.FromResult<object>(repository.Delete(id));
            public Task<object> DeleteAsync(IEnumerable<string> ids) => Task.FromResult<object>(repository.Delete(ids));
            public Task<object> DeleteAsync(DynamicFilter filter) => throw new NotSupportedException();
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
            public IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => new FindFluentStub<T>(Queryable);
            public IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null) => new FindFluentStub<T>(Queryable);
            public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => new FindFluentStub<T>(Queryable.Where(filter));
            public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null) => Task.FromResult<IAsyncCursor<T>>(new AsyncCursorStub<T>(Queryable));
            public Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null) => Task.FromResult<IAsyncCursor<T>>(new AsyncCursorStub<T>(Queryable));
            public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null) => Task.FromResult<IAsyncCursor<T>>(new AsyncCursorStub<T>(Queryable.Where(filter)));
            public T? Get(string id, IClientSessionHandle? session = null) => _items.TryGetValue(id, out var entity) ? entity : null;
            public Task<T?> GetAsync(string id, IClientSessionHandle? session = null) => Task.FromResult(Get(id, session));
            public long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Queryable.LongCount(filter);
            public long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Queryable.LongCount();
            public Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null) => throw new NotSupportedException();
            public Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Task.FromResult(Count(filter, session, options));
            public Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null) => Task.FromResult(Count(filter, session, options));
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
            public UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => new UpdateResult.Acknowledged(0, 0, new BsonDocument());
            public Task<UpdateResult> UpdateManyAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null) => Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(0, 0, new BsonDocument()));
            public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null)
            {
                _items[EnsureId(entity).Id] = entity;
                return null!;
            }
            public Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null) => Task.FromResult(Replace(entity, session));
            public DeleteResult Delete(string id, IClientSessionHandle? session = null)
            {
                _items.Remove(id);
                return null!;
            }
            public DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null)
            {
                foreach (var id in ids.ToList()) _items.Remove(id);
                return null!;
            }
            public DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null)
            {
                _items.Clear();
                return null!;
            }
            public Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null) => Task.FromResult(Delete(id, session));
            public Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null) => Task.FromResult(Delete(ids, session));
            public Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null) => throw new NotSupportedException();
            public Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null) => Task.FromResult(Delete(filter, session));
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
            public Task<List<BsonValue>> DistinctFieldValuesAsync(DynamicFilter filter, string field, IClientSessionHandle? session = null) => Task.FromResult(new List<BsonValue>());
        }

        private sealed class FindFluentStub<T>(IQueryable<T> data) : IFindFluent<T, T>
        {
            public FilterDefinition<T> Filter { get; set; } = Builders<T>.Filter.Empty;
            public FindOptions<T, T> Options { get; } = new();
            public IFindFluent<T, TNewProjection> As<TNewProjection>(IBsonSerializer<TNewProjection> resultSerializer) => throw new NotSupportedException();
            public long Count(CancellationToken cancellationToken = default) => data.LongCount();
            public Task<long> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Count(cancellationToken));
            public long CountDocuments(CancellationToken cancellationToken = default) => data.LongCount();
            public Task<long> CountDocumentsAsync(CancellationToken cancellationToken = default) => Task.FromResult(CountDocuments(cancellationToken));
            public IFindFluent<T, T> Limit(int? limit)
            {
                data = limit.HasValue ? data.Take(limit.Value) : data;
                return this;
            }
            public IFindFluent<T, TNewProjection> Project<TNewProjection>(ProjectionDefinition<T, TNewProjection> projection) => throw new NotSupportedException();
            public IFindFluent<T, T> Skip(int? skip)
            {
                data = skip.HasValue ? data.Skip(skip.Value) : data;
                return this;
            }
            public IFindFluent<T, T> Sort(SortDefinition<T> sort) => this;
            public IAsyncCursor<T> ToCursor(CancellationToken cancellationToken = default) => new AsyncCursorStub<T>(data);
            public Task<IAsyncCursor<T>> ToCursorAsync(CancellationToken cancellationToken = default) => Task.FromResult(ToCursor(cancellationToken));
            public string ToString(ExpressionTranslationOptions translationOptions) => ToString() ?? string.Empty;
        }

        private sealed class AsyncCursorStub<T>(IEnumerable<T> data) : IAsyncCursor<T>
        {
            private bool _moved;
            public IEnumerable<T> Current { get; private set; } = [];
            public void Dispose() { }
            public bool MoveNext(CancellationToken cancellationToken = default)
            {
                if (_moved)
                {
                    Current = [];
                    return false;
                }

                _moved = true;
                Current = data.ToList();
                return Current.Any();
            }
            public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNext(cancellationToken));
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
    }
}
