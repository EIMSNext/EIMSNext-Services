using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Auth.Entities;
using EIMSNext.Core;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;

using HKH.Mef2.Integration;

using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class AdminGroupApiService(IResolver resolver) : ApiServiceBase<AdminGroup, AdminGroupViewModel, IAdminGroupService>(resolver)
    {
        public async Task<AdminGroup?> Move(MoveAdminGroupRequest request)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有移动管理组的权限");

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return null;
            }

            var moving = CoreService.All().FirstOrDefault(x =>
                x.Id == request.Id &&
                x.CorpId == IdentityContext.CurrentCorpId &&
                !x.DeleteFlag &&
                x.Type != AdminGroupType.System);

            if (moving == null)
            {
                return null;
            }

            var parentId = request.ParentId?.Trim() ?? string.Empty;
            if (moving.Type == AdminGroupType.Folder && !string.IsNullOrEmpty(parentId))
            {
                throw new ArgumentException("管理分组只能位于根级");
            }

            if (!string.IsNullOrEmpty(parentId))
            {
                EnsureFolderExists(parentId);
            }

            var siblings = CoreService.All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.Type != AdminGroupType.System &&
                    x.Id != moving.Id &&
                    x.ParentId == parentId)
                .OrderBy(x => x.SortValue)
                .ThenBy(x => x.Id)
                .Select(x => new AdminGroupSortItem(x))
                .ToList();

            var previous = SortHelper.FindSibling(siblings, request.PreviousId);
            var next = SortHelper.FindSibling(siblings, request.NextId);
            if (!string.IsNullOrWhiteSpace(request.PreviousId) && previous == null)
            {
                throw new ArgumentException("前一个同级节点不存在");
            }

            if (!string.IsNullOrWhiteSpace(request.NextId) && next == null)
            {
                throw new ArgumentException("后一个同级节点不存在");
            }

            var movingItem = new AdminGroupSortItem(moving);
            var sortValue = SortHelper.CalculateSortValue(previous?.SortValue, next?.SortValue);
            moving.ParentId = parentId;
            if (sortValue.HasValue)
            {
                moving.SortValue = sortValue.Value;
                await CoreService.ReplaceAsync(moving);
            }
            else
            {
                var normalized = SortHelper.NormalizeWithMoving(siblings, movingItem, previous?.Id, next?.Id);
                foreach (var sibling in normalized)
                {
                    await CoreService.ReplaceAsync(sibling.Group);
                }
            }

            return moving;
        }

        protected override async Task AddAsyncCore(AdminGroup entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有创建管理组的权限");

            entity.EmployeeIds = NormalizeIds(entity.EmployeeIds);
            entity.AppIds = NormalizeIds(entity.AppIds);
            entity.AppDepartmentIds = NormalizeIds(entity.AppDepartmentIds);
            entity.AppRoleIds = NormalizeIds(entity.AppRoleIds);
            entity.ContactDepartmentIds = NormalizeIds(entity.ContactDepartmentIds);
            entity.ContactRoleIds = NormalizeIds(entity.ContactRoleIds);
            entity.ParentId = entity.ParentId?.Trim() ?? string.Empty;

            if (entity.Type == AdminGroupType.System)
            {
                throw new ArgumentException("系统管理员组只能由企业创建流程生成");
            }

            ValidateAndNormalize(entity, original: null);
            await base.AddAsyncCore(entity);
        }

        protected override async Task<ReplaceOneResult> ReplaceAsyncCore(AdminGroup entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有修改管理组的权限");

            var original = await CoreService.GetAsync(entity.Id) ?? throw new ArgumentException("管理组不存在");
            if (original.CorpId != IdentityContext.CurrentCorpId || original.DeleteFlag)
            {
                throw new ArgumentException("管理组不存在");
            }

            if (entity.Type != original.Type)
            {
                throw new ArgumentException("不能修改管理组类型");
            }

            entity.CorpId = original.CorpId;
            entity.EmployeeIds = NormalizeIds(entity.EmployeeIds);
            entity.AppIds = NormalizeIds(entity.AppIds);
            entity.AppDepartmentIds = NormalizeIds(entity.AppDepartmentIds);
            entity.AppRoleIds = NormalizeIds(entity.AppRoleIds);
            entity.ContactDepartmentIds = NormalizeIds(entity.ContactDepartmentIds);
            entity.ContactRoleIds = NormalizeIds(entity.ContactRoleIds);
            entity.ParentId = entity.ParentId?.Trim() ?? string.Empty;

            ValidateAndNormalize(entity, original);
            return await base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有删除管理组的权限");

            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            foreach (var id in idList)
            {
                var group = await CoreService.GetAsync(id);
                if (group == null || group.CorpId != IdentityContext.CurrentCorpId || group.DeleteFlag)
                {
                    continue;
                }

                if (group.Type == AdminGroupType.System)
                {
                    throw new ArgumentException("系统管理员组不能删除");
                }

                if (group.Type == AdminGroupType.Folder && CoreService.All().Any(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    x.ParentId == group.Id))
                {
                    throw new ArgumentException("当前分组下存在管理组，不能删除");
                }
            }

            return await base.DeleteAsyncCore(idList);
        }

        private void ValidateAndNormalize(AdminGroup entity, AdminGroup? original)
        {
            switch (entity.Type)
            {
                case AdminGroupType.System:
                    ValidateSystemGroup(entity, original);
                    break;
                case AdminGroupType.Folder:
                    ValidateFolder(entity);
                    break;
                case AdminGroupType.Normal:
                    ValidateNormalGroup(entity);
                    break;
                default:
                    throw new ArgumentException("管理组类型无效");
            }
        }

        private void ValidateSystemGroup(AdminGroup entity, AdminGroup? original)
        {
            if (original == null)
            {
                throw new ArgumentException("系统管理员组只能由企业创建流程生成");
            }

            if (entity.EmployeeIds.Count > 5)
            {
                throw new ArgumentException("系统管理员不能超过5人");
            }

            if (!IsSystemOnlyEmployeeIdsChanged(entity, original))
            {
                throw new ArgumentException("系统管理员组只允许更新管理员");
            }

            entity.Name = original.Name;
            entity.Description = original.Description;
            entity.ParentId = string.Empty;
            entity.SortValue = original.SortValue;
            entity.AppIds = [];
            entity.CanCreateOrDeleteApp = false;
            entity.AppDepartmentScopeMode = ScopeMode.All;
            entity.AppDepartmentIds = [];
            entity.AppRoleScopeMode = ScopeMode.All;
            entity.AppRoleIds = [];
            entity.ContactDepartmentPermission = PermissionLevel.None;
            entity.ContactDepartmentScopeMode = ScopeMode.All;
            entity.ContactDepartmentIds = [];
            entity.ContactRolePermission = PermissionLevel.None;
            entity.ContactRoleScopeMode = ScopeMode.All;
            entity.ContactRoleIds = [];

            ValidateEmployeeIds(entity);
            EnsureNoNormalMembershipConflict(entity.EmployeeIds, entity.Id);
        }

        private static bool IsSystemOnlyEmployeeIdsChanged(AdminGroup entity, AdminGroup original)
        {
            return entity.Name == original.Name &&
                entity.Description == original.Description &&
                (entity.ParentId ?? string.Empty) == (original.ParentId ?? string.Empty) &&
                entity.SortValue == original.SortValue &&
                SameIds(entity.AppIds, original.AppIds) &&
                entity.CanCreateOrDeleteApp == original.CanCreateOrDeleteApp &&
                entity.AppDepartmentScopeMode == original.AppDepartmentScopeMode &&
                SameIds(entity.AppDepartmentIds, original.AppDepartmentIds) &&
                entity.AppRoleScopeMode == original.AppRoleScopeMode &&
                SameIds(entity.AppRoleIds, original.AppRoleIds) &&
                entity.ContactDepartmentPermission == original.ContactDepartmentPermission &&
                entity.ContactDepartmentScopeMode == original.ContactDepartmentScopeMode &&
                SameIds(entity.ContactDepartmentIds, original.ContactDepartmentIds) &&
                entity.ContactRolePermission == original.ContactRolePermission &&
                entity.ContactRoleScopeMode == original.ContactRoleScopeMode &&
                SameIds(entity.ContactRoleIds, original.ContactRoleIds);
        }

        private void ValidateFolder(AdminGroup entity)
        {
            if (!string.IsNullOrWhiteSpace(entity.ParentId))
            {
                throw new ArgumentException("管理分组只能位于根级");
            }

            if (entity.EmployeeIds.Count > 0 ||
                entity.AppIds.Count > 0 ||
                entity.CanCreateOrDeleteApp ||
                entity.AppDepartmentScopeMode != ScopeMode.All ||
                entity.AppDepartmentIds.Count > 0 ||
                entity.AppRoleScopeMode != ScopeMode.All ||
                entity.AppRoleIds.Count > 0 ||
                entity.ContactDepartmentPermission != PermissionLevel.None ||
                entity.ContactDepartmentScopeMode != ScopeMode.All ||
                entity.ContactDepartmentIds.Count > 0 ||
                entity.ContactRolePermission != PermissionLevel.None ||
                entity.ContactRoleScopeMode != ScopeMode.All ||
                entity.ContactRoleIds.Count > 0)
            {
                throw new ArgumentException("管理分组不能保存员工、应用或权限配置");
            }

            entity.EmployeeIds = [];
            entity.AppIds = [];
            entity.CanCreateOrDeleteApp = false;
            entity.AppDepartmentScopeMode = ScopeMode.All;
            entity.AppDepartmentIds = [];
            entity.AppRoleScopeMode = ScopeMode.All;
            entity.AppRoleIds = [];
            entity.ContactDepartmentPermission = PermissionLevel.None;
            entity.ContactDepartmentScopeMode = ScopeMode.All;
            entity.ContactDepartmentIds = [];
            entity.ContactRolePermission = PermissionLevel.None;
            entity.ContactRoleScopeMode = ScopeMode.All;
            entity.ContactRoleIds = [];
        }

        private void ValidateNormalGroup(AdminGroup entity)
        {
            if (!string.IsNullOrWhiteSpace(entity.ParentId))
            {
                EnsureFolderExists(entity.ParentId);
            }

            ValidateEmployeeIds(entity);
            EnsureNoSystemMembershipConflict(entity.EmployeeIds, entity.Id);
            ClearAllScopeIds(entity);
            ValidateReferenceIds(entity);
        }

        private void EnsureFolderExists(string parentId)
        {
            var exists = CoreService.All().Any(x =>
                x.Id == parentId &&
                x.CorpId == IdentityContext.CurrentCorpId &&
                !x.DeleteFlag &&
                x.Type == AdminGroupType.Folder);

            if (!exists)
            {
                throw new ArgumentException("父级分组不存在");
            }
        }

        private void ValidateEmployeeIds(AdminGroup entity)
        {
            if (entity.EmployeeIds.Count == 0)
            {
                return;
            }

            var employees = Resolver.GetService<IEmployeeService, Employee>().All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    entity.EmployeeIds.Contains(x.Id))
                .ToList();

            var validEmployeeIds = employees.Where(x => !x.IsDummy).Select(x => x.Id).ToHashSet();
            var invalidIds = entity.EmployeeIds.Where(x => !validEmployeeIds.Contains(x)).ToList();
            if (invalidIds.Count > 0)
            {
                throw new ArgumentException("管理组包含无效员工");
            }

            var userIds = employees.Select(x => x.UserId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var ownerUserIds = Resolver.GetService<User>().All()
                .Where(x => userIds.Contains(x.Id) && x.Crops.Any(c => c.CorpId == IdentityContext.CurrentCorpId && c.IsCorpOwner))
                .Select(x => x.Id)
                .ToHashSet();

            if (employees.Any(x => ownerUserIds.Contains(x.UserId)))
            {
                throw new ArgumentException("企业Owner不能加入任何管理组");
            }
        }

        private void EnsureNoSystemMembershipConflict(IEnumerable<string> employeeIds, string currentGroupId)
        {
            var idSet = employeeIds.ToHashSet();
            if (idSet.Count == 0)
            {
                return;
            }

            var conflict = CoreService.All().Any(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                !x.DeleteFlag &&
                x.Id != currentGroupId &&
                x.Type == AdminGroupType.System &&
                x.EmployeeIds.Any(id => idSet.Contains(id)));

            if (conflict)
            {
                throw new ArgumentException("员工不能同时加入系统管理员组和普通管理组");
            }
        }

        private void EnsureNoNormalMembershipConflict(IEnumerable<string> employeeIds, string currentGroupId)
        {
            var idSet = employeeIds.ToHashSet();
            if (idSet.Count == 0)
            {
                return;
            }

            var conflict = CoreService.All().Any(x =>
                x.CorpId == IdentityContext.CurrentCorpId &&
                !x.DeleteFlag &&
                x.Id != currentGroupId &&
                x.Type == AdminGroupType.Normal &&
                x.EmployeeIds.Any(id => idSet.Contains(id)));

            if (conflict)
            {
                throw new ArgumentException("员工不能同时加入系统管理员组和普通管理组");
            }
        }

        private void ValidateReferenceIds(AdminGroup entity)
        {
            EnsureIdsExist<AppDef>(entity.AppIds, "应用");
            EnsureIdsExist<Department>(entity.AppDepartmentIds, "应用内可选部门");
            EnsureIdsExist<Department>(entity.ContactDepartmentIds, "通讯录部门");
            EnsureIdsExist<Role>(entity.AppRoleIds, "应用内可选角色");
            EnsureIdsExist<Role>(entity.ContactRoleIds, "通讯录角色");
        }

        private void EnsureIdsExist<T>(IEnumerable<string> ids, string name) where T : Core.Entities.CorpEntityBase
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (idList.Count == 0)
            {
                return;
            }

            var existingIds = Resolver.GetService<T>().All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .Select(x => x.Id)
                .ToHashSet();

            if (idList.Any(x => !existingIds.Contains(x)))
            {
                throw new ArgumentException($"{name}包含无效数据");
            }
        }

        private static void ClearAllScopeIds(AdminGroup entity)
        {
            if (entity.AppDepartmentScopeMode == ScopeMode.All)
            {
                entity.AppDepartmentIds = [];
            }

            if (entity.AppRoleScopeMode == ScopeMode.All)
            {
                entity.AppRoleIds = [];
            }

            if (entity.ContactDepartmentScopeMode == ScopeMode.All)
            {
                entity.ContactDepartmentIds = [];
            }

            if (entity.ContactRoleScopeMode == ScopeMode.All)
            {
                entity.ContactRoleIds = [];
            }
        }

        private static List<string> NormalizeIds(IEnumerable<string>? ids)
        {
            return (ids ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
        }

        private static bool SameIds(IEnumerable<string>? left, IEnumerable<string>? right)
        {
            return NormalizeIds(left).OrderBy(x => x).SequenceEqual(NormalizeIds(right).OrderBy(x => x));
        }

        private class AdminGroupSortItem(AdminGroup group) : ISortItem
        {
            public AdminGroup Group { get; } = group;

            public string Id => Group.Id;

            public int SortValue
            {
                get => Group.SortValue;
                set => Group.SortValue = value;
            }
        }
    }
}
