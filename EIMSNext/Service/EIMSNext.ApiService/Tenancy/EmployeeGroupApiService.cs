using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;

using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class EmployeeGroupApiService(IResolver resolver) : ApiServiceBase<EmployeeGroup, EmployeeGroupViewModel, IEmployeeGroupService>(resolver)
    {
        public async Task AddEmployeesToEmployeeGroup(AddEmployeesToEmployeeGroupRequest request)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageEmployeeGroupMembers(request.EmployeeGroupId!, request.EmpIds ?? []);
            var employeeGroup = CoreService.Get(request.EmployeeGroupId!);
            if (employeeGroup != null)
            {
                var empService = Resolver.GetService<IEmployeeService, Employee>();
                await empService.AddToEmployeeGroupAsync(employeeGroup, request.EmpIds!);
            }
        }

        public async Task RemoveEmployeesFromEmployeeGroup(RemoveEmployeesFromEmployeeGroupRequest request)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageEmployeeGroupMembers(request.EmployeeGroupId!, request.EmpIds ?? []);
            var empService = Resolver.GetService<IEmployeeService, Employee>();
            await empService.RemoveFromEmployeeGroupAsync(request.EmployeeGroupId!, request.EmpIds!);
        }

        public async Task<bool> Move(MoveEmployeeGroupTreeNodeRequest request)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureUnrestrictedManagement("没有修改员工组结构的权限");

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return false;
            }

            var employeeGroupCategoryService = Resolver.GetService<IEmployeeGroupCategoryService, EmployeeGroupCategory>();
            var employeeGroupCategoryId = request.EmployeeGroupCategoryId?.Trim() ?? string.Empty;
            if (request.IsGroup && !string.IsNullOrEmpty(employeeGroupCategoryId))
            {
                throw new ArgumentException("员工组分类只能位于根级");
            }

            if (!string.IsNullOrEmpty(employeeGroupCategoryId))
            {
                var parentExists = employeeGroupCategoryService.All().Any(x =>
                    x.Id == employeeGroupCategoryId &&
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag);

                if (!parentExists)
                {
                    throw new ArgumentException("员工组分类不存在");
                }
            }

            if (request.IsGroup)
            {
                var movingGroup = employeeGroupCategoryService.All().FirstOrDefault(x =>
                    x.Id == request.Id &&
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag);

                if (movingGroup == null)
                {
                    return false;
                }

                var siblings = LoadEmployeeGroupRootNodes(employeeGroupCategoryService, CoreService, movingGroup.Id);
                await MoveEmployeeGroupNode(siblings, new EmployeeGroupSortNode(movingGroup), request.PreviousId, request.NextId, employeeGroupCategoryService, CoreService);
                return true;
            }

            var movingEmployeeGroup = CoreService.All().FirstOrDefault(x =>
                x.Id == request.Id &&
                x.CorpId == IdentityContext.CurrentCorpId &&
                !x.DeleteFlag);

            if (movingEmployeeGroup == null)
            {
                return false;
            }

            var employeeGroupSiblings = LoadEmployeeGroupSiblingNodes(employeeGroupCategoryService, CoreService, employeeGroupCategoryId, movingEmployeeGroup.Id);
            movingEmployeeGroup.EmployeeGroupCategoryId = employeeGroupCategoryId;
            await MoveEmployeeGroupNode(employeeGroupSiblings, new EmployeeGroupSortNode(movingEmployeeGroup), request.PreviousId, request.NextId, employeeGroupCategoryService, CoreService);
            return true;
        }

        private async Task MoveEmployeeGroupNode(
            List<EmployeeGroupSortNode> siblings,
            EmployeeGroupSortNode moving,
            string? previousId,
            string? nextId,
            IEmployeeGroupCategoryService groupService,
            IEmployeeGroupService employeeGroupService)
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
                await ReplaceNode(moving, groupService, employeeGroupService);
            }
            else
            {
                var normalized = SortHelper.NormalizeWithMoving(siblings, moving, previous?.Id, next?.Id);
                foreach (var node in normalized)
                {
                    await ReplaceNode(node, groupService, employeeGroupService);
                }
            }
        }

        protected override Task AddAsyncCore(EmployeeGroup entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureUnrestrictedManagement("没有创建员工组的权限");
            EnsureEmployeeGroupCategoryBelongsToCurrentCorp(entity.EmployeeGroupCategoryId);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(EmployeeGroup entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureUnrestrictedManagement("没有修改员工组的权限");
            EnsureEmployeeGroupCategoryBelongsToCurrentCorp(entity.EmployeeGroupCategoryId);
            return base.ReplaceAsyncCore(entity);
        }

        protected override Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureUnrestrictedManagement("没有删除员工组的权限");

            var employeeGroupIds = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var referenced = Resolver.GetRepository<Employee>().Queryable
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag)
                .Any(x => x.EmployeeGroups.Any(r => employeeGroupIds.Contains(r.EmployeeGroupId)));
            if (referenced)
            {
                throw new BadRequestException("该员工组有员工使用，不能删除");
            }

            return base.DeleteAsyncCore(ids);
        }

        private void EnsureEmployeeGroupCategoryBelongsToCurrentCorp(string? employeeGroupCategoryId)
        {
            if (string.IsNullOrWhiteSpace(employeeGroupCategoryId))
            {
                return;
            }

            var group = Resolver.GetRepository<EmployeeGroupCategory>().Get(employeeGroupCategoryId);
            if (group == null || group.DeleteFlag || group.CorpId != IdentityContext.CurrentCorpId)
            {
                throw new BadRequestException("员工组分类不存在或不属于当前企业");
            }
        }

        private List<EmployeeGroupSortNode> LoadEmployeeGroupRootNodes(IEmployeeGroupCategoryService groupService, IEmployeeGroupService employeeGroupService, string movingId)
        {
            var groups = groupService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.Id != movingId)
                .Select(x => new EmployeeGroupSortNode(x))
                .ToList();

            var employeeGroups = employeeGroupService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.EmployeeGroupCategoryId == string.Empty && x.Id != movingId)
                .Select(x => new EmployeeGroupSortNode(x))
                .ToList();

            return groups.Concat(employeeGroups).OrderBy(x => x.SortValue).ThenBy(x => x.Id).ToList();
        }

        private List<EmployeeGroupSortNode> LoadEmployeeGroupSiblingNodes(IEmployeeGroupCategoryService groupService, IEmployeeGroupService employeeGroupService, string employeeGroupCategoryId, string movingId)
        {
            if (!string.IsNullOrWhiteSpace(employeeGroupCategoryId))
            {
                return employeeGroupService.All()
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && x.EmployeeGroupCategoryId == employeeGroupCategoryId && x.Id != movingId)
                    .Select(x => new EmployeeGroupSortNode(x))
                    .OrderBy(x => x.SortValue)
                    .ThenBy(x => x.Id)
                    .ToList();
            }

            return LoadEmployeeGroupRootNodes(groupService, employeeGroupService, movingId);
        }

        private static async Task ReplaceNode(EmployeeGroupSortNode node, IEmployeeGroupCategoryService groupService, IEmployeeGroupService employeeGroupService)
        {
            if (node.Group != null)
            {
                await groupService.ReplaceAsync(node.Group);
            }
            else if (node.EmployeeGroup != null)
            {
                await employeeGroupService.ReplaceAsync(node.EmployeeGroup);
            }
        }

        private class EmployeeGroupSortNode : ISortItem
        {
            public EmployeeGroupSortNode(EmployeeGroupCategory group)
            {
                Group = group;
            }

            public EmployeeGroupSortNode(EmployeeGroup employeeGroup)
            {
                EmployeeGroup = employeeGroup;
            }

            public EmployeeGroupCategory? Group { get; }

            public EmployeeGroup? EmployeeGroup { get; }

            public string Id => Group?.Id ?? EmployeeGroup!.Id;

            public int SortValue
            {
                get => Group?.SortValue ?? EmployeeGroup!.SortValue;
                set
                {
                    if (Group != null)
                    {
                        Group.SortValue = value;
                    }
                    else if (EmployeeGroup != null)
                    {
                        EmployeeGroup.SortValue = value;
                    }
                }
            }
        }
    }
}

