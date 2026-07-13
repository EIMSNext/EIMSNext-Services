using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    public class AdminPermissionEvaluator(IResolver resolver) : ApiServiceBase(resolver)
    {
        // 注意：此类的实例生命周期必须是 Scoped (per-request)，由 MEF2 容器保证。
        // 下方的 _normalGroups / _snapshot 缓存在单次 HTTP 请求内复用，
        // 以减少对 AdminGroup 的重复查询；切勿改为 Singleton，否则会跨请求泄漏用户权限数据。
        private IReadOnlyList<AdminGroup>? _normalGroups;
        private AdminPermissionSnapshot? _snapshot;

        public bool HasUnrestrictedManagementIdentity =>
            IdentityContext.IdentityType == IdentityType.CorpOwmer ||
            IdentityContext.IdentityType == IdentityType.CorpAdmin ||
            IdentityContext.IdentityType == IdentityType.System ||
            IdentityContext.IdentityType == IdentityType.Client;

        public bool ShouldApplyNormalAdminRules => IdentityContext.IdentityType == IdentityType.AppAdmin;

        public AdminPermissionSnapshot GetSnapshot()
        {
            if (_snapshot != null)
            {
                return _snapshot;
            }

            var groups = LoadNormalGroups();
            var snapshot = new AdminPermissionSnapshot
            {
                IsNormalAdmin = groups.Count > 0,
                CanCreateOrDeleteApp = groups.Any(x => x.CanCreateOrDeleteApp),
                ManageableAppIds = Normalize(groups.SelectMany(x => x.AppIds)),
                DeletableAppIds = Normalize(groups.Where(x => x.CanCreateOrDeleteApp).SelectMany(x => x.AppIds)),
            };

            ApplyScope(
                snapshot,
                ExpandDepartmentScope(CombineScope(groups, x => x.AppDepartmentScopeMode, x => x.AppDepartmentIds)),
                static (x, mode) => x.AppDepartmentScopeMode = mode,
                static (x, ids) => x.AppDepartmentIds = ids);

            ApplyScope(
                snapshot,
                CombineScope(groups, x => x.AppRoleScopeMode, x => x.AppRoleIds),
                static (x, mode) => x.AppRoleScopeMode = mode,
                static (x, ids) => x.AppRoleIds = ids);

            ApplyScope(
                snapshot,
                ExpandDepartmentScope(CombineScope(groups.Where(x => x.ContactDepartmentPermission >= PermissionLevel.View), x => x.ContactDepartmentScopeMode, x => x.ContactDepartmentIds)),
                static (x, mode) => x.ContactViewDepartmentScopeMode = mode,
                static (x, ids) => x.ContactViewDepartmentIds = ids);

            ApplyScope(
                snapshot,
                ExpandDepartmentScope(CombineScope(groups.Where(x => x.ContactDepartmentPermission >= PermissionLevel.Manage), x => x.ContactDepartmentScopeMode, x => x.ContactDepartmentIds)),
                static (x, mode) => x.ContactManageDepartmentScopeMode = mode,
                static (x, ids) => x.ContactManageDepartmentIds = ids);

            ApplyScope(
                snapshot,
                CombineScope(groups.Where(x => x.ContactRolePermission >= PermissionLevel.View), x => x.ContactRoleScopeMode, x => x.ContactRoleIds),
                static (x, mode) => x.ContactViewRoleScopeMode = mode,
                static (x, ids) => x.ContactViewRoleIds = ids);

            ApplyScope(
                snapshot,
                CombineScope(groups.Where(x => x.ContactRolePermission >= PermissionLevel.Manage), x => x.ContactRoleScopeMode, x => x.ContactRoleIds),
                static (x, mode) => x.ContactManageRoleScopeMode = mode,
                static (x, ids) => x.ContactManageRoleIds = ids);

            _snapshot = snapshot;
            return snapshot;
        }

        public List<string> GetUsageAppIdsForCurrentEmployee()
        {
            var memberScope = GetCurrentEmployeeMemberScope();
            if (memberScope == null) return [];
            var empId = memberScope.EmployeeId;
            var roleIds = memberScope.RoleIds.ToList();
            var deptIds = memberScope.DepartmentIds.ToList();
            var ancestorDeptIds = memberScope.AncestorDepartmentIds.ToList();

            var authGroupAppIds = Resolver.GetService<AuthGroup>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.Members.Any(m =>
                        (m.Type == MemberType.Employee && m.Id == empId) ||
                        (m.Type == MemberType.Role && roleIds.Contains(m.Id)) ||
                        (m.Type == MemberType.Department && ((m.CascadedDept && ancestorDeptIds.Contains(m.Id)) || deptIds.Contains(m.Id)))))
                .Select(x => x.AppId)
                .Distinct()
                .ToList();

            return authGroupAppIds
                .Concat(GetPublishedDashboardAppIds(memberScope))
                .Distinct()
                .ToList();
        }

        public List<string> GetUsageFormIdsForCurrentEmployee(string? appId)
        {
            var memberScope = GetCurrentEmployeeMemberScope();
            if (memberScope == null) return [];
            var empId = memberScope.EmployeeId;
            var roleIds = memberScope.RoleIds.ToList();
            var deptIds = memberScope.DepartmentIds.ToList();
            var ancestorDeptIds = memberScope.AncestorDepartmentIds.ToList();

            return Resolver.GetService<AuthGroup>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    (string.IsNullOrEmpty(appId) || x.AppId == appId) &&
                    x.Members.Any(m =>
                        (m.Type == MemberType.Employee && m.Id == empId) ||
                        (m.Type == MemberType.Role && roleIds.Contains(m.Id)) ||
                        (m.Type == MemberType.Department && ((m.CascadedDept && ancestorDeptIds.Contains(m.Id)) || deptIds.Contains(m.Id)))))
                .Select(x => x.FormId)
                .Distinct()
                .ToList();
        }

        public List<string> GetUsageDashboardIdsForCurrentEmployee(string? appId)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return Resolver.GetService<DashboardDef>()
                    .Query(x =>
                        x.CorpId == IdentityContext.CurrentCorpId &&
                        !x.DeleteFlag &&
                        (string.IsNullOrEmpty(appId) || x.AppId == appId))
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();
            }

            var memberScope = GetCurrentEmployeeMemberScope();
            if (memberScope == null) return [];

            var empId = memberScope.EmployeeId;
            var roleIds = memberScope.RoleIds.ToList();
            var deptIds = memberScope.DepartmentIds.ToList();
            var ancestorDeptIds = memberScope.AncestorDepartmentIds.ToList();
            var manageableAppIds = ShouldApplyNormalAdminRules ? GetSnapshot().ManageableAppIds : new List<string>();
            return Resolver.GetService<DashboardDef>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    (string.IsNullOrEmpty(appId) || x.AppId == appId) &&
                    (manageableAppIds.Contains(x.AppId) ||
                     (x.MemberPublishEnabled && x.PublishMembers.Any(m =>
                         (m.Type == MemberType.Employee && m.Id == empId) ||
                         (m.Type == MemberType.Role && roleIds.Contains(m.Id)) ||
                         (m.Type == MemberType.Department && ((m.CascadedDept && ancestorDeptIds.Contains(m.Id)) || deptIds.Contains(m.Id)))))))
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        public List<AuthGroup> GetUsageAuthGroupsForCurrentEmployee(string? formId)
        {
            var employee = IdentityContext.CurrentEmployee as Employee;
            if (employee == null)
            {
                return [];
            }

            var empId = employee.Id;
            var roleIds = employee.Roles.Select(x => x.RoleId).ToList();
            var deptIds = GetCurrentEmployeeDeptIds();
            var ancestorDeptIds = GetCurrentEmployeeAncestorDepartmentIds(deptIds);

            return Resolver.GetService<AuthGroup>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    !x.Disabled &&
                    (string.IsNullOrEmpty(formId) || x.FormId == formId) &&
                    x.Members.Any(m =>
                        (m.Type == MemberType.Employee && m.Id == empId) ||
                        (m.Type == MemberType.Role && roleIds.Contains(m.Id)) ||
                        (m.Type == MemberType.Department && ((m.CascadedDept && ancestorDeptIds.Contains(m.Id)) || deptIds.Contains(m.Id)))))
                .ToList();
        }

        public List<AppMenuPermissionItem> GetAppMenuPermissions(string appId)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return [];
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                var formIds = GetUsageFormIdsForCurrentEmployee(appId);
                var isManagedApp = IsAppManageable(appId);
                if (isManagedApp)
                {
                    formIds = Resolver.GetService<FormDef>()
                        .Query(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.AppId == appId)
                        .Select(x => x.Id)
                        .Distinct()
                        .ToList();
                }

                var dashIds = GetUsageDashboardIdsForCurrentEmployee(appId);

                return BuildAppMenuPermissions(formIds, dashIds);
            }

            if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                var formIds = GetUsageFormIdsForCurrentEmployee(appId);
                var dashIds = GetUsageDashboardIdsForCurrentEmployee(appId);

                return BuildAppMenuPermissions(formIds, dashIds);
            }

            return [];
        }

        public void EnsureCanCreateApp()
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return;
            }

            if (!ShouldApplyNormalAdminRules || !GetSnapshot().CanCreateOrDeleteApp)
            {
                throw new ForbiddenException("没有新建应用权限");
            }
        }

        public void EnsureUnrestrictedManagement(string message)
        {
            if (!HasUnrestrictedManagementIdentity)
            {
                throw new ForbiddenException(message);
            }
        }

        public void EnsureCanManageApp(string? appId)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new BadRequestException("应用ID不能为空");
            }

            if (!ShouldApplyNormalAdminRules || !GetSnapshot().ManageableAppIds.Contains(appId))
            {
                throw new ForbiddenException("没有管理该应用的权限");
            }
        }

        public void EnsureCanDeleteApp(string? appId)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new BadRequestException("应用ID不能为空");
            }

            if (!ShouldApplyNormalAdminRules || !GetSnapshot().DeletableAppIds.Contains(appId))
            {
                throw new ForbiddenException("没有删除该应用的权限");
            }
        }

        public void EnsureCanManageAuthGroup(AuthGroup entity)
        {
            EnsureCanManageApp(entity.AppId);

            if (!ShouldApplyNormalAdminRules)
            {
                return;
            }

            var snapshot = GetSnapshot();
            foreach (var member in entity.Members ?? [])
            {
                switch (member.Type)
                {
                    case MemberType.Department:
                        EnsureDepartmentInScope(member.Id, snapshot.AppDepartmentScopeMode, snapshot.AppDepartmentIds, "应用成员包含无权选择的部门");
                        break;
                    case MemberType.Employee:
                        EnsureEmployeeDepartmentInScope(member.Id, snapshot.AppDepartmentScopeMode, snapshot.AppDepartmentIds, "应用成员包含无权选择的员工");
                        break;
                    case MemberType.Role:
                        EnsureRoleInScope(member.Id, snapshot.AppRoleScopeMode, snapshot.AppRoleIds, "应用成员包含无权选择的角色");
                        break;
                }
            }
        }

        public void EnsureCanManageEmployee(Employee entity, Employee? original = null)
        {
            EnsureCanManageEmployee(entity, original, null);
        }

        public void EnsureCanManageEmployee(Employee entity, Employee? original, IEnumerable<string>? targetDepartmentIds)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return;
            }

            if (!ShouldApplyNormalAdminRules)
            {
                throw new ForbiddenException("没有管理员工权限");
            }

            var snapshot = GetSnapshot();
            var employeeDeptRepo = Resolver.GetRepository<EmployeeDepartment>();

            if (original != null)
            {
                var originalDeptIds = employeeDeptRepo.Queryable
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == original.Id)
                    .Select(x => x.DepartmentId)
                    .Distinct()
                    .ToList();
                foreach (var deptId in originalDeptIds)
                {
                    EnsureDepartmentInScope(deptId, snapshot.ContactManageDepartmentScopeMode, snapshot.ContactManageDepartmentIds, "没有管理该员工的权限");
                }
            }

            var entityDeptIds = targetDepartmentIds?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList()
                ?? employeeDeptRepo.Queryable
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == entity.Id)
                    .Select(x => x.DepartmentId)
                    .Distinct()
                    .ToList();
            foreach (var deptId in entityDeptIds)
            {
                EnsureDepartmentInScope(deptId, snapshot.ContactManageDepartmentScopeMode, snapshot.ContactManageDepartmentIds, "没有管理该员工的权限");
            }
        }

        public void EnsureCanManageEmployees(IEnumerable<string> employeeIds)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return;
            }

            if (!ShouldApplyNormalAdminRules)
            {
                throw new ForbiddenException("没有管理员工权限");
            }

            var idList = employeeIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (idList.Count == 0)
            {
                return;
            }

            var employees = Resolver.GetService<Employee>()
                .Query(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (employees.Count != idList.Count)
            {
                throw new BadRequestException("员工不存在");
            }

            var snapshot = GetSnapshot();
            var employeeDeptRepo = Resolver.GetRepository<EmployeeDepartment>();
            foreach (var employee in employees)
            {
                var deptIds = employeeDeptRepo.Queryable
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == employee.Id)
                    .Select(x => x.DepartmentId)
                    .Distinct()
                    .ToList();

                foreach (var deptId in deptIds)
                {
                    EnsureDepartmentInScope(deptId, snapshot.ContactManageDepartmentScopeMode, snapshot.ContactManageDepartmentIds, "没有管理该员工的权限");
                }
            }
        }

        public void EnsureCanManageRoleMembers(string roleId, IEnumerable<string> employeeIds)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return;
            }

            if (!ShouldApplyNormalAdminRules)
            {
                throw new ForbiddenException("没有管理角色成员权限");
            }

            var snapshot = GetSnapshot();
            EnsureRoleInScope(roleId, snapshot.ContactManageRoleScopeMode, snapshot.ContactManageRoleIds, "没有管理该角色成员的权限");
            EnsureCanManageEmployees(employeeIds);
        }

        public async Task SyncCreatedAppToNormalAdminGroupsAsync(string appId)
        {
            if (!ShouldApplyNormalAdminRules || string.IsNullOrWhiteSpace(appId))
            {
                return;
            }

            var adminGroupService = Resolver.GetService<IAdminGroupService, AdminGroup>();
            var employeeId = (IdentityContext.CurrentEmployee as Employee)?.Id;
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return;
            }

            var groups = adminGroupService.All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.Type == AdminGroupType.Normal &&
                    x.CanCreateOrDeleteApp &&
                    x.EmployeeIds.Contains(employeeId))
                .ToList();

            foreach (var group in groups)
            {
                if (!group.AppIds.Contains(appId))
                {
                    group.AppIds.Add(appId);
                    group.AppIds = Normalize(group.AppIds);
                    await adminGroupService.ReplaceAsync(group);
                }
            }
        }

        public IQueryable<Department> FilterDepartmentsForAdminScope(IQueryable<Department> query)
        {
            if (!ShouldApplyNormalAdminRules)
            {
                return query;
            }

            var snapshot = GetSnapshot();
            return FilterByScope(query, snapshot.ContactViewDepartmentScopeMode, snapshot.ContactViewDepartmentIds);
        }

        public IQueryable<Employee> FilterEmployeesForAdminScope(IQueryable<Employee> query)
        {
            if (!ShouldApplyNormalAdminRules)
            {
                return query;
            }

            var snapshot = GetSnapshot();
            return FilterEmployeesByDepartmentScope(query, snapshot.ContactViewDepartmentScopeMode, snapshot.ContactViewDepartmentIds);
        }

        public IQueryable<Role> FilterRolesForAdminScope(IQueryable<Role> query)
        {
            if (!ShouldApplyNormalAdminRules)
            {
                return query;
            }

            var snapshot = GetSnapshot();
            return FilterByScope(query, snapshot.ContactViewRoleScopeMode, snapshot.ContactViewRoleIds);
        }

        public bool IsAppManageable(string appId)
        {
            if (HasUnrestrictedManagementIdentity)
            {
                return true;
            }

            return ShouldApplyNormalAdminRules && GetSnapshot().ManageableAppIds.Contains(appId);
        }

        private IReadOnlyList<AdminGroup> LoadNormalGroups()
        {
            if (_normalGroups != null)
            {
                return _normalGroups;
            }

            var employeeId = (IdentityContext.CurrentEmployee as Employee)?.Id;
            if (string.IsNullOrWhiteSpace(employeeId) || string.IsNullOrWhiteSpace(IdentityContext.CurrentCorpId))
            {
                _normalGroups = [];
                return _normalGroups;
            }

            _normalGroups = Resolver.GetService<IAdminGroupService, AdminGroup>()
                .All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.Type == AdminGroupType.Normal &&
                    x.EmployeeIds.Contains(employeeId))
                .ToList();

            return _normalGroups;
        }

        private List<string> GetCurrentEmployeeDeptIds()
        {
            var employee = IdentityContext.CurrentEmployee as Employee;
            if (employee == null)
            {
                return [];
            }

            return Resolver.GetRepository<EmployeeDepartment>().Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == employee.Id)
                .Select(x => x.DepartmentId)
                .Distinct()
                .ToList();
        }

        private List<string> GetCurrentEmployeeAncestorDepartmentIds(IEnumerable<string> deptIds)
        {
            var idList = deptIds.ToList();
            if (idList.Count == 0)
            {
                return [];
            }

            var allDepts = Resolver.GetService<Department>()
                .Query(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag)
                .Select(x => new { x.Id, x.HeriarchyId })
                .ToList();
            var hierarchyIds = allDepts
                .Where(x => idList.Contains(x.Id))
                .Select(x => x.HeriarchyId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return allDepts
                .Where(x => hierarchyIds.Any(h => h.Contains($"|{x.Id}|")))
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        private EmployeeMemberScope? GetCurrentEmployeeMemberScope()
        {
            var employee = IdentityContext.CurrentEmployee as Employee;
            if (employee == null)
            {
                return null;
            }

            var deptIds = GetCurrentEmployeeDeptIds();
            return new EmployeeMemberScope(
                employee.Id,
                employee.Roles.Select(x => x.RoleId).ToHashSet(),
                deptIds.ToHashSet(),
                GetCurrentEmployeeAncestorDepartmentIds(deptIds).ToHashSet());
        }

        private List<string> GetPublishedDashboardAppIds(EmployeeMemberScope memberScope)
        {
            var empId = memberScope.EmployeeId;
            var roleIds = memberScope.RoleIds.ToList();
            var deptIds = memberScope.DepartmentIds.ToList();
            var ancestorDeptIds = memberScope.AncestorDepartmentIds.ToList();

            return Resolver.GetService<DashboardDef>()
                .Query(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.MemberPublishEnabled &&
                    x.PublishMembers.Any(m =>
                        (m.Type == MemberType.Employee && m.Id == empId) ||
                        (m.Type == MemberType.Role && roleIds.Contains(m.Id)) ||
                        (m.Type == MemberType.Department && ((m.CascadedDept && ancestorDeptIds.Contains(m.Id)) || deptIds.Contains(m.Id)))))
                .Select(x => x.AppId)
                .Distinct()
                .ToList();
        }

        private void EnsureEmployeeDepartmentInScope(string employeeId, string scopeMode, IEnumerable<string> departmentIds, string message)
        {
            var employee = Resolver.GetService<Employee>()
                .Query(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.Id == employeeId)
                .FirstOrDefault();

            if (employee == null)
            {
                throw new BadRequestException("员工不存在");
            }

            var empDeptIds = Resolver.GetRepository<EmployeeDepartment>().Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == employeeId)
                .Select(x => x.DepartmentId)
                .Distinct()
                .ToList();

            if (empDeptIds.Count == 0)
            {
                throw new BadRequestException("员工未分配部门");
            }

            foreach (var deptId in empDeptIds)
            {
                EnsureDepartmentInScope(deptId, scopeMode, departmentIds, message);
            }
        }

        private void EnsureDepartmentInScope(string departmentId, string scopeMode, IEnumerable<string> departmentIds, string message)
        {
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                throw new BadRequestException("部门ID不能为空");
            }

            EnsureEntityExists<Department>(departmentId, "部门不存在");
            if (!IsInScope(departmentId, scopeMode, departmentIds))
            {
                throw new ForbiddenException(message);
            }
        }

        private void EnsureRoleInScope(string roleId, string scopeMode, IEnumerable<string> roleIds, string message)
        {
            if (string.IsNullOrWhiteSpace(roleId))
            {
                throw new BadRequestException("角色ID不能为空");
            }

            EnsureEntityExists<Role>(roleId, "角色不存在");
            if (!IsInScope(roleId, scopeMode, roleIds))
            {
                throw new ForbiddenException(message);
            }
        }

        private void EnsureEntityExists<T>(string id, string message) where T : CorpEntityBase
        {
            var exists = Resolver.GetService<T>()
                .All()
                .Any(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.Id == id);

            if (!exists)
            {
                throw new BadRequestException(message);
            }
        }

        private static bool IsInScope(string id, string scopeMode, IEnumerable<string> ids)
        {
            return scopeMode == AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All) || ids.Contains(id);
        }

        private static IQueryable<T> FilterByScope<T>(IQueryable<T> query, string scopeMode, IEnumerable<string> ids) where T : CorpEntityBase
        {
            if (scopeMode == AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All))
            {
                return query;
            }

            var idList = ids.ToList();
            return idList.Count == 0 ? query.Where(x => false) : query.Where(x => idList.Contains(x.Id));
        }

        private IQueryable<Employee> FilterEmployeesByDepartmentScope(IQueryable<Employee> query, string scopeMode, IEnumerable<string> departmentIds)
        {
            if (scopeMode == AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All))
            {
                return query;
            }

            var idList = departmentIds.ToList();
            if (idList.Count == 0)
            {
                return query.Where(x => false);
            }

            var empIds = Resolver.GetRepository<EmployeeDepartment>().Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && idList.Contains(x.DepartmentId))
                .Select(x => x.EmployeeId)
                .Distinct()
                .ToList();

            return query.Where(x => empIds.Contains(x.Id));
        }

        private static PermissionScope CombineScope(
            IEnumerable<AdminGroup> groups,
            Func<AdminGroup, ScopeMode> modeSelector,
            Func<AdminGroup, IEnumerable<string>> idsSelector)
        {
            var list = groups.ToList();
            if (list.Count == 0)
            {
                return new PermissionScope(false, []);
            }

            if (list.Any(x => modeSelector(x) == ScopeMode.All))
            {
                return new PermissionScope(true, []);
            }

            return new PermissionScope(false, Normalize(list.SelectMany(idsSelector)));
        }

        private PermissionScope ExpandDepartmentScope(PermissionScope scope)
        {
            if (scope.All || scope.Ids.Count == 0)
            {
                return scope;
            }

            var expandedIds = GetDepartmentAndDescendantIds(scope.Ids);
            return new PermissionScope(false, expandedIds.Count == 0 ? scope.Ids : expandedIds);
        }

        private static void ApplyScope(
            AdminPermissionSnapshot snapshot,
            PermissionScope scope,
            Action<AdminPermissionSnapshot, string> modeSetter,
            Action<AdminPermissionSnapshot, List<string>> idsSetter)
        {
            modeSetter(snapshot, AdminPermissionSnapshot.ToWireScopeMode(scope.All ? ScopeMode.All : ScopeMode.Partial));
            idsSetter(snapshot, scope.Ids);
        }

        private static List<string> Normalize(IEnumerable<string>? ids)
        {
            return (ids ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
        }

        private List<string> GetDepartmentAndDescendantIds(IEnumerable<string> departmentIds)
        {
            var idList = Normalize(departmentIds);
            if (idList.Count == 0)
            {
                return [];
            }

            var allDepts = Resolver.GetService<Department>()
                .Query(x => x.CorpId == IdentityContext.CurrentCorpId
                    && !x.DeleteFlag)
                .Select(x => new { x.Id, x.HeriarchyId })
                .ToList();

            return allDepts
                .Where(x => idList.Any(id => x.Id == id || x.HeriarchyId.Contains($"|{id}|")))
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        private static List<AppMenuPermissionItem> BuildAppMenuPermissions(IEnumerable<string> formIds, IEnumerable<string> dashIds)
        {
            return formIds
                .Distinct()
                .Select(x => new AppMenuPermissionItem { Id = x, Type = FormType.Form })
                .Concat(dashIds.Distinct().Select(x => new AppMenuPermissionItem { Id = x, Type = FormType.Dashboard }))
                .ToList();
        }

        private sealed record PermissionScope(bool All, List<string> Ids);

        private sealed record EmployeeMemberScope(
            string EmployeeId,
            HashSet<string> RoleIds,
            HashSet<string> DepartmentIds,
            HashSet<string> AncestorDepartmentIds)
        {
            public bool Matches(Member member)
            {
                return (member.Type == MemberType.Employee && member.Id == EmployeeId) ||
                       (member.Type == MemberType.Role && RoleIds.Contains(member.Id)) ||
                       (member.Type == MemberType.Department &&
                        ((member.CascadedDept && AncestorDepartmentIds.Contains(member.Id)) || DepartmentIds.Contains(member.Id)));
            }
        }
    }
}
