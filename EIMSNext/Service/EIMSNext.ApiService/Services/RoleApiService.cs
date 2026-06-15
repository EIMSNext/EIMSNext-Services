using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class RoleApiService(IResolver resolver) : ApiServiceBase<Role, RoleViewModel, IRoleService>(resolver)
    {
        public async Task AddEmployeesToRole(AddEmpsToRoleRequest request)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageRoleMembers(request.RoleId!, request.EmpIds ?? []);
            var role = CoreService.Get(request.RoleId!);
            if (role != null)
            {
                var empService = Resolver.GetService<IEmployeeService, Employee>();
                await empService.AddToRoleAsync(role, request.EmpIds!);
            }
        }

        public async Task RemoveEmployeesFromRole(RemoveEmpsToRoleRequest request)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageRoleMembers(request.RoleId!, request.EmpIds ?? []);
            var empService = Resolver.GetService<IEmployeeService, Employee>();
            await empService.RemoveFromRoleAsync(request.RoleId!, request.EmpIds!);
        }

        public async Task<bool> Move(MoveRoleTreeNodeRequest request)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有修改角色结构的权限");

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return false;
            }

            var roleGroupService = Resolver.GetService<IRoleGroupService, RoleGroup>();
            var roleGroupId = request.RoleGroupId?.Trim() ?? string.Empty;
            if (request.IsGroup && !string.IsNullOrEmpty(roleGroupId))
            {
                throw new ArgumentException("角色组只能位于根级");
            }

            if (!string.IsNullOrEmpty(roleGroupId))
            {
                var parentExists = roleGroupService.All().Any(x =>
                    x.Id == roleGroupId &&
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag);

                if (!parentExists)
                {
                    throw new ArgumentException("角色组不存在");
                }
            }

            if (request.IsGroup)
            {
                var movingGroup = roleGroupService.All().FirstOrDefault(x =>
                    x.Id == request.Id &&
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag);

                if (movingGroup == null)
                {
                    return false;
                }

                var siblings = LoadRoleRootNodes(roleGroupService, CoreService, movingGroup.Id);
                await MoveRoleNode(siblings, new RoleSortNode(movingGroup), request.PreviousId, request.NextId, roleGroupService, CoreService);
                return true;
            }

            var movingRole = CoreService.All().FirstOrDefault(x =>
                x.Id == request.Id &&
                x.CorpId == IdentityContext.CurrentCorpId &&
                !x.DeleteFlag);

            if (movingRole == null)
            {
                return false;
            }

            var roleSiblings = LoadRoleSiblingNodes(roleGroupService, CoreService, roleGroupId, movingRole.Id);
            movingRole.RoleGroupId = roleGroupId;
            await MoveRoleNode(roleSiblings, new RoleSortNode(movingRole), request.PreviousId, request.NextId, roleGroupService, CoreService);
            return true;
        }

        private async Task MoveRoleNode(
            List<RoleSortNode> siblings,
            RoleSortNode moving,
            string? previousId,
            string? nextId,
            IRoleGroupService groupService,
            IRoleService roleService)
        {
            var previous = SortHelper.FindSibling(siblings, previousId);
            var next = SortHelper.FindSibling(siblings, nextId);
            if (!string.IsNullOrWhiteSpace(previousId) && previous == null)
            {
                throw new ArgumentException("前一个同级节点不存在");
            }

            if (!string.IsNullOrWhiteSpace(nextId) && next == null)
            {
                throw new ArgumentException("后一个同级节点不存在");
            }

            var sortValue = SortHelper.CalculateSortValue(previous?.SortValue, next?.SortValue);
            if (sortValue.HasValue)
            {
                moving.SortValue = sortValue.Value;
                await ReplaceNode(moving, groupService, roleService);
            }
            else
            {
                var normalized = SortHelper.NormalizeWithMoving(siblings, moving, previous?.Id, next?.Id);
                foreach (var node in normalized)
                {
                    await ReplaceNode(node, groupService, roleService);
                }
            }
        }

        protected override Task AddAsyncCore(Role entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有创建角色的权限");
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(Role entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有修改角色的权限");
            return base.ReplaceAsyncCore(entity);
        }

        protected override Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureUnrestrictedManagement("没有删除角色的权限");
            return base.DeleteAsyncCore(ids);
        }

        private List<RoleSortNode> LoadRoleRootNodes(IRoleGroupService groupService, IRoleService roleService, string movingId)
        {
            var groups = groupService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.Id != movingId)
                .Select(x => new RoleSortNode(x))
                .ToList();

            var roles = roleService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.RoleGroupId == string.Empty && x.Id != movingId)
                .Select(x => new RoleSortNode(x))
                .ToList();

            return groups.Concat(roles).OrderBy(x => x.SortValue).ThenBy(x => x.Id).ToList();
        }

        private List<RoleSortNode> LoadRoleSiblingNodes(IRoleGroupService groupService, IRoleService roleService, string roleGroupId, string movingId)
        {
            if (!string.IsNullOrWhiteSpace(roleGroupId))
            {
                return roleService.All()
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.RoleGroupId == roleGroupId && x.Id != movingId)
                    .Select(x => new RoleSortNode(x))
                    .OrderBy(x => x.SortValue)
                    .ThenBy(x => x.Id)
                    .ToList();
            }

            return LoadRoleRootNodes(groupService, roleService, movingId);
        }

        private static async Task ReplaceNode(RoleSortNode node, IRoleGroupService groupService, IRoleService roleService)
        {
            if (node.Group != null)
            {
                await groupService.ReplaceAsync(node.Group);
            }
            else if (node.Role != null)
            {
                await roleService.ReplaceAsync(node.Role);
            }
        }

        private class RoleSortNode : ISortItem
        {
            public RoleSortNode(RoleGroup group)
            {
                Group = group;
            }

            public RoleSortNode(Role role)
            {
                Role = role;
            }

            public RoleGroup? Group { get; }

            public Role? Role { get; }

            public string Id => Group?.Id ?? Role!.Id;

            public int SortValue
            {
                get => Group?.SortValue ?? Role!.SortValue;
                set
                {
                    if (Group != null)
                    {
                        Group.SortValue = value;
                    }
                    else if (Role != null)
                    {
                        Role.SortValue = value;
                    }
                }
            }
        }
    }
}
