using System.Composition.Hosting;
using System.Linq.Expressions;

using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using EIMSNext.TestSupport;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class EmployeeDepartmentRelationshipTests
    {
        private const string CorpId = "corp-employee-dept";

        private InMemoryRepository<Employee> _employeeRepo = null!;
        private InMemoryRepository<EmployeeDepartment> _employeeDepartmentRepo = null!;
        private InMemoryRepository<EmployeeGroupMember> _employeeGroupMemberRepo = null!;
        private InMemoryRepository<Department> _departmentRepo = null!;
        private InMemoryRepository<FormDataPermissionGroup> _permissionGroupRepo = null!;
        private InMemoryRepository<TenantAdminGroup> _adminGroupRepo = null!;
        private FakeIdentityContext _identityContext = null!;
        private EmployeeApiService _employeeApiService = null!;
        private TestableDepartmentService _departmentService = null!;
        private TenantAccessEvaluator _permissionEvaluator = null!;
        private FormDataReadScopeResolver _readScopeResolver = null!;

        [TestInitialize]
        public void Init()
        {
            _employeeRepo = new InMemoryRepository<Employee>();
            _employeeDepartmentRepo = new InMemoryRepository<EmployeeDepartment>();
            _employeeGroupMemberRepo = new InMemoryRepository<EmployeeGroupMember>();
            _departmentRepo = new InMemoryRepository<Department>();
            _permissionGroupRepo = new InMemoryRepository<FormDataPermissionGroup>();
            _adminGroupRepo = new InMemoryRepository<TenantAdminGroup>();

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
                [typeof(IRepository<EmployeeGroupMember>)] = _employeeGroupMemberRepo,
                [typeof(IRepository<Department>)] = _departmentRepo,
                [typeof(IRepository<FormDataPermissionGroup>)] = _permissionGroupRepo,
                [typeof(IRepository<TenantAdminGroup>)] = _adminGroupRepo,
                [typeof(IRepository<Corporate>)] = corporateRepo,
                [typeof(IRepository<AuditLog>)] = new InMemoryRepository<AuditLog>(),
                [typeof(IEmployeeService)] = new FakeEmployeeService(_employeeRepo),
                [typeof(IFormDataPermissionGroupService)] = new FakeFormDataPermissionGroupService(_permissionGroupRepo),
                [typeof(ITenantAdminGroupService)] = new FakeTenantAdminGroupService(_adminGroupRepo),
                [typeof(IService<Corporate>)] = new FakeEntityService<Corporate>(corporateRepo),
                [typeof(IService<User>)] = new FakeEntityService<User>(new InMemoryRepository<User>()),
                [typeof(IService<FormDataPermissionGroup>)] = new FakeEntityService<FormDataPermissionGroup>(_permissionGroupRepo),
                [typeof(IService<TenantAdminGroup>)] = new FakeEntityService<TenantAdminGroup>(_adminGroupRepo),
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

            services[typeof(IEmployeeAccessSubjectResolver)] = new FakeEmployeeAccessSubjectResolver(
                _identityContext,
                _employeeDepartmentRepo,
                _employeeGroupMemberRepo,
                _departmentRepo);

            var resolver = new TestResolver(services);
            _permissionEvaluator = new TenantAccessEvaluator(resolver);
            services[typeof(TenantAccessEvaluator)] = _permissionEvaluator;
            _readScopeResolver = new FormDataReadScopeResolver(resolver);
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
        public async Task AddEmployee_WithMultipleDepartments_SavesRelations()
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
                _departmentService.InvokeBeforeDeleteAsync(x => x.Id == parent.Id));
        }

        [TestMethod]
        public void FilterByDepartment_WithCascadedParent_ReturnsChildDepartmentEmployees()
        {
            var parent = SeedDepartment("dept-filter-parent", "Parent");
            var child = SeedDepartment("dept-filter-child", "Child", parent.Id);
            var sibling = SeedDepartment("dept-filter-sibling", "Sibling");
            var childEmployee = new EmployeeViewModel { Id = "emp-child", CorpId = CorpId, Code = "E004", EmpName = "Child Employee" };
            var siblingEmployee = new EmployeeViewModel { Id = "emp-sibling", CorpId = CorpId, Code = "E005", EmpName = "Sibling Employee" };
            _employeeDepartmentRepo.Insert(new EmployeeDepartment { CorpId = CorpId, EmployeeId = childEmployee.Id, DepartmentId = child.Id, HeriarchyId = child.HeriarchyId });
            _employeeDepartmentRepo.Insert(new EmployeeDepartment { CorpId = CorpId, EmployeeId = siblingEmployee.Id, DepartmentId = sibling.Id, HeriarchyId = sibling.HeriarchyId });

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
        public void FormDataPermissionGroup_WithCascadedParentDepartment_ReturnsChildDepartmentEmployee()
        {
            var parent = SeedDepartment("dept-auth-parent", "总部");
            var child = SeedDepartment("dept-auth-child", "研发部", parent.Id);
            var employee = SeedEmployee("emp-auth-child", "Child Employee", child.Id);
            _identityContext.IdentityTypeValue = IdentityType.Employee;
            _identityContext.CurrentEmployeeValue = employee;

            _permissionGroupRepo.Insert(new FormDataPermissionGroup
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
        public void UsageFormDataPermissionGroups_ForAppAdmin_MatchesCurrentEmployeeMembershipOnly()
        {
            var parent = SeedDepartment("dept-auth-scope-parent", "总部");
            var child = SeedDepartment("dept-auth-scope-child", "研发部", parent.Id);
            var employee = SeedEmployee("emp-auth-scope", "Scoped Admin", child.Id);
            _employeeGroupMemberRepo.Insert(new EmployeeGroupMember
            {
                CorpId = CorpId,
                EmployeeId = employee.Id,
                EmployeeGroupId = "role-auth-scope",
                EmployeeGroupName = "业务角色"
            });
            _identityContext.IdentityTypeValue = IdentityType.AppAdmin;
            _identityContext.CurrentEmployeeValue = employee;

            _permissionGroupRepo.Insert(
            [
                CreateFormDataPermissionGroup("auth-by-employee", new Member { Id = employee.Id, Type = MemberType.Employee }),
                CreateFormDataPermissionGroup("auth-by-role", new Member { Id = "role-auth-scope", Type = MemberType.EmployeeGroup }),
                CreateFormDataPermissionGroup("auth-by-direct-dept", new Member { Id = child.Id, Type = MemberType.Department }),
                CreateFormDataPermissionGroup("auth-by-cascaded-dept", new Member { Id = parent.Id, Type = MemberType.Department, CascadedDept = true }),
                CreateFormDataPermissionGroup("auth-unassigned", new Member { Id = "another-employee", Type = MemberType.Employee }),
                CreateFormDataPermissionGroup("auth-disabled", new Member { Id = employee.Id, Type = MemberType.Employee }, disabled: true),
                CreateFormDataPermissionGroup("auth-other-form", new Member { Id = employee.Id, Type = MemberType.Employee }, formId: "form-002")
            ]);

            var result = _permissionEvaluator.GetUsageFormDataPermissionGroupsForCurrentEmployee("form-001")
                .Select(x => x.Id)
                .ToList();

            CollectionAssert.AreEquivalent(
                new[] { "auth-by-employee", "auth-by-role", "auth-by-direct-dept", "auth-by-cascaded-dept" },
                result);
        }

        [TestMethod]
        public void FormDataReadScope_ForUnrestrictedAdmin_HonorsExplicitFormDataPermissionGroup()
        {
            var department = SeedDepartment("dept-auth-read-scope", "研发部");
            var employee = SeedEmployee("emp-auth-read-scope", "Corp Admin", department.Id);
            _identityContext.IdentityTypeValue = IdentityType.CorpAdmin;
            _identityContext.CurrentEmployeeValue = employee;
            _permissionGroupRepo.Insert(CreateFormDataPermissionGroup(
                "auth-read-scope",
                new Member { Id = employee.Id, Type = MemberType.Employee }));

            var unrestrictedScope = _readScopeResolver.Resolve("form-001");
            var selectedGroupScope = _readScopeResolver.Resolve("form-001", "auth-read-scope");
            var unassignedGroupScope = _readScopeResolver.Resolve("form-001", "auth-unassigned");

            Assert.IsTrue(unrestrictedScope.CanRead);
            Assert.IsNull(unrestrictedScope.DataFilter);
            Assert.IsTrue(selectedGroupScope.CanRead);
            Assert.AreEqual(Fields.CreateById, selectedGroupScope.DataFilter?.Field);
            Assert.AreEqual(employee.Id, selectedGroupScope.DataFilter?.Value);
            Assert.IsFalse(unassignedGroupScope.CanRead);
        }

        [TestMethod]
        public async Task AddEmployee_NormalAdminWithParentManageScope_AllowsChildDepartment()
        {
            var parent = SeedDepartment("dept-manage-parent", "总部");
            var child = SeedDepartment("dept-manage-child", "研发部", parent.Id);
            var admin = SeedEmployee("emp-admin", "Admin", parent.Id);
            _identityContext.IdentityTypeValue = IdentityType.AppAdmin;
            _identityContext.CurrentEmployeeValue = admin;
            _adminGroupRepo.Insert(new TenantAdminGroup
            {
                Id = "admin-group",
                CorpId = CorpId,
                Type = TenantAdminGroupType.Normal,
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
            _adminGroupRepo.Insert(new TenantAdminGroup
            {
                Id = "admin-group-denied",
                CorpId = CorpId,
                Type = TenantAdminGroupType.Normal,
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

        [TestMethod]
        public async Task ReplaceDepartment_SyncsRelationHeriarchySnapshot()
        {
            var root = SeedDepartment("dept-snap-root", "Root");
            var otherRoot = SeedDepartment("dept-snap-other", "Other");
            var parent = SeedDepartment("dept-snap-parent", "Parent", root.Id);
            var child = SeedDepartment("dept-snap-child", "Child", parent.Id);

            // 员工分别挂在 child 与 otherRoot 下；child 将被移动到 otherRoot 下。
            var inChild = SeedEmployee("emp-snap-in-child", "In Child", child.Id);
            var inOther = SeedEmployee("emp-snap-in-other", "In Other", otherRoot.Id);
            Assert.IsNotNull(inChild);
            Assert.IsNotNull(inOther);

            // 模拟移动：child 换父级到 otherRoot。
            child.ParentId = otherRoot.Id;
            await _departmentService.InvokeReplaceAsync(child);

            // 关系表快照：child 自身与它的下级（此处无）都要跟随新路径；otherRoot 下的不受影响。
            var childRelation = _employeeDepartmentRepo.Queryable.Single(x => x.EmployeeId == inChild.Id);
            Assert.AreEqual($"{otherRoot.HeriarchyId}{child.Id}|", childRelation.HeriarchyId);

            var otherRelation = _employeeDepartmentRepo.Queryable.Single(x => x.EmployeeId == inOther.Id);
            Assert.AreEqual(otherRoot.HeriarchyId, otherRelation.HeriarchyId);
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

        private static FormDataPermissionGroup CreateFormDataPermissionGroup(string id, Member member, bool disabled = false, string formId = "form-001")
        {
            return new FormDataPermissionGroup
            {
                Id = id,
                CorpId = CorpId,
                AppId = "app-001",
                FormId = formId,
                Members = [member],
                Disabled = disabled
            };
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
            var department = _departmentRepo.Get(departmentId);
            _employeeDepartmentRepo.Insert(new EmployeeDepartment
            {
                CorpId = CorpId,
                EmployeeId = employee.Id,
                DepartmentId = departmentId,
                HeriarchyId = department?.HeriarchyId ?? string.Empty
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
            public Task InvokeBeforeDeleteAsync(Expression<Func<Department, bool>> filter) => BeforeDelete(filter);

            public async Task InvokeReplaceAsync(Department department)
            {
                await BeforeReplace(department);
                Resolver.GetRepository<Department>().Replace(department);
                await AfterReplace(department);
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
            public Task<int> AddToEmployeeGroupAsync(EmployeeGroup role, IEnumerable<string> empIds) => throw new NotSupportedException();
            public Task<int> RemoveFromEmployeeGroupAsync(string employeeGroupId, IEnumerable<string> empIds) => throw new NotSupportedException();
            public Task ReviewJoinCorporateAsync(IEnumerable<string> employeeIds, bool approved, string corpId) => throw new NotSupportedException();
            public Task AcceptInviteAsync(string userId, string? phone, string? email, bool accepted) => throw new NotSupportedException();
        }

        private sealed class FakeEmployeeAccessSubjectResolver(
            FakeIdentityContext identityContext,
            InMemoryRepository<EmployeeDepartment> employeeDepartmentRepository,
            InMemoryRepository<EmployeeGroupMember> employeeGroupMemberRepository,
            InMemoryRepository<Department> departmentRepository) : IEmployeeAccessSubjectResolver
        {
            public EmployeeAccessSubjects ResolveCurrent()
            {
                var employee = identityContext.CurrentEmployeeValue;
                if (employee == null)
                {
                    return EmployeeAccessSubjects.Empty;
                }

                var departmentIds = employeeDepartmentRepository.Queryable
                    .Where(x => x.EmployeeId == employee.Id)
                    .Select(x => x.DepartmentId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var ancestorDepartmentIds = departmentRepository.Queryable
                    .Where(x => departmentIds.Contains(x.Id))
                    .SelectMany(x => x.HeriarchyId.Split('|', StringSplitOptions.RemoveEmptyEntries))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return new EmployeeAccessSubjects(
                    employee.Id,
                    departmentIds,
                    ancestorDepartmentIds,
                    employeeGroupMemberRepository.Queryable
                        .Where(x => x.EmployeeId == employee.Id)
                        .Select(x => x.EmployeeGroupId)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase));
            }
        }

        private sealed class FakeFormDataPermissionGroupService(InMemoryRepository<FormDataPermissionGroup> repository)
            : FakeEntityService<FormDataPermissionGroup>(repository), IFormDataPermissionGroupService
        {
        }

        private sealed class FakeTenantAdminGroupService(InMemoryRepository<TenantAdminGroup> repository)
            : FakeEntityService<TenantAdminGroup>(repository), ITenantAdminGroupService
        {
        }

        /// <summary>
        /// 读路径由共享的 <see cref="StubEntityService{T}"/> 基于内存仓储兜底。
        /// </summary>
        private class FakeEntityService<T>(InMemoryRepository<T> repository) : StubEntityService<T>, IService<T>
            where T : class, IEntityKey
        {
            public override T? Get(string id) => repository.Get(id);

            public override IQueryable<T> All() => repository.Queryable;

            public override IQueryable<T> Query(Expression<Func<T, bool>> where) => repository.Queryable.Where(where);

            public override long Count(Expression<Func<T, bool>> filter) => repository.Queryable.LongCount(filter);

            public override bool Exists(Expression<Func<T, bool>> where) => repository.Queryable.Any(where);

            public override void Add(T entity) => repository.Insert(entity);

            public override void Add(IEnumerable<T> entities) => repository.Insert(entities);

            public override int Replace(T entity)
            {
                // 仓储层的 Replace 是「跟踪后提交」，不返回受影响行数；
                // 受影响行数由 Service 层语义决定——内存仓储里按主键是否已存在折算。
                var affected = repository.Get(entity.Id) is null ? 0 : 1;
                repository.Replace(entity);
                return affected;
            }

            public override int Delete(string id) => repository.Delete(id);

            public override int Delete(IEnumerable<string> ids) => repository.Delete(ids);

            public override Task<T?> GetAsync(string id) => repository.GetAsync(id);

            public override Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
                => repository.FindAsync(filter, cancellationToken);

            public override Task<long> CountAsync(Expression<Func<T, bool>> filter) => Task.FromResult(Count(filter));

            public override Task<bool> ExistsAsync(Expression<Func<T, bool>> where) => Task.FromResult(Exists(where));

            public override Task AddAsync(T entity) => repository.InsertAsync(entity);

            public override Task AddAsync(IEnumerable<T> entities) => repository.InsertAsync(entities);

            public override async Task<int> ReplaceAsync(T entity)
            {
                var affected = repository.Get(entity.Id) is null ? 0 : 1;
                await repository.ReplaceAsync(entity).ConfigureAwait(false);
                return affected;
            }

            public override Task<int> DeleteAsync(string id) => repository.DeleteAsync(id);

            public override Task<int> DeleteAsync(IEnumerable<string> ids) => repository.DeleteAsync(ids);
        }

        private sealed class InMemoryRepository<T> : StubRepository<T> where T : class, IEntityKey
        {
            private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);
            private int _nextId;

            public override IQueryable<T> Queryable => _items.Values.AsQueryable();

            public override T? Get(string id) => _items.TryGetValue(id, out var entity) ? entity : null;

            public override Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(id));

            public override long Count(Expression<Func<T, bool>> predicate) => Queryable.LongCount(predicate);

            public override Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
                => Task.FromResult(Queryable.Where(filter).ToList());

            public override IQueryable<T> Find(Expression<Func<T, bool>> filter) => Queryable.Where(filter);

            public override IQueryable<T> Find(DynamicFilter filter) => Find(filter.ToPredicate<T>());

            public override IQueryable<T> Find(DynamicFindOptions<T> options) => Find(options.ToQueryFindOptions<T>());

            public override IQueryable<T> Find(QueryFindOptions<T> options) => Apply(options, Queryable);

            public override Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default)
                => Task.FromResult(Find(options).ToList());

            public override Task<List<T>> FindAsync(QueryFindOptions<T> options, CancellationToken cancellationToken = default)
                => Task.FromResult(Apply(options, Queryable).ToList());

            public override Task<List<T>> FindAsync(DynamicFilter filter, CancellationToken cancellationToken = default)
                => Task.FromResult(Find(filter).ToList());

            public override List<T> FindList(DynamicFilter filter) => Find(filter).ToList();

            /// <summary>
            /// 内存版 <see cref="QueryFindOptions{T}"/> 应用：过滤 + 分页。
            /// </summary>
            /// <remarks>
            /// 动态排序（<see cref="QueryFindOptions{T}.Sort"/>）是「字段名字符串」驱动的，
            /// EF 侧由 <c>EF.Property</c> 翻译，内存桩无法忠实模拟，
            /// 因此一旦被测路径真的用到排序就<b>显式报错</b>，避免给出看似正确、实则顺序错误的假结果。
            /// </remarks>
            private static IQueryable<T> Apply(QueryFindOptions<T> options, IQueryable<T> source)
            {
                ArgumentNullException.ThrowIfNull(options);

                if (options.Sort is { IsEmpty: false })
                {
                    throw new NotSupportedException("内存桩不支持动态排序（QueryFindOptions.Sort）。");
                }

                var query = options.Filter is null ? source : source.Where(options.Filter);
                var skip = options.GetEffectiveSkip();
                if (skip > 0)
                {
                    query = query.Skip(skip);
                }

                return query.Take(options.GetEffectiveTake());
            }

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

            public override int Delete(IEnumerable<string> ids)
            {
                var affected = 0;
                foreach (var id in ids.ToList())
                {
                    if (_items.Remove(id))
                    {
                        affected++;
                    }
                }

                return affected;
            }

            public override Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default)
                => Task.FromResult(Delete(id));

            public override Task<int> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
                => Task.FromResult(Delete(ids));

            public override Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            {
                var removed = Queryable.Where(predicate).ToList();
                var affected = 0;
                foreach (var entity in removed)
                {
                    if (_items.Remove(entity.Id))
                    {
                        affected++;
                    }
                }

                return Task.FromResult(affected);
            }

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
    }
}
