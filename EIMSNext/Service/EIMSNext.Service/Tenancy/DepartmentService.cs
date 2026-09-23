using System.Linq.Expressions;
using HKH.Mef2.Integration;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Common;
using System.Text.RegularExpressions;

namespace EIMSNext.Service
{
    public class DepartmentService(IResolver resolver) : EntityServiceBase<Department>(resolver), IDepartmentService
    {
        private IRepository<Employee> EmployeeRepository => Resolver.GetRepository<Employee>();

        protected override Task BeforeAdd(IEnumerable<Department> entities)
        {
            foreach (var entity in entities)
            {
                Repository.EnsureId(entity);

                if (!string.IsNullOrEmpty(entity.ParentId))
                {
                    var parent = Repository.Get(entity.ParentId);
                    if (parent == null)
                    {
                        entity.ParentId = "";
                        entity.HeriarchyId = $"|{entity.Id}|";
                        entity.HeriarchyName = entity.Name;
                    }
                    else if (!string.Equals(parent.CorpId, entity.CorpId, StringComparison.Ordinal))
                    {
                        throw new BadRequestException("上级部门必须属于当前企业");
                    }
                    else
                    {
                        entity.HeriarchyId = $"{parent.HeriarchyId}{entity.Id}|";
                        entity.HeriarchyName = $"{entity.Name}/{parent.HeriarchyName}";
                    }
                }
                else
                {
                    entity.HeriarchyId = $"|{entity.Id}|";
                    entity.HeriarchyName = entity.Name;
                }
            }

            return base.BeforeAdd(entities);
        }

        protected override Task BeforeReplace(Department entity)
        {
            NormalizeHierarchy(entity);
            return base.BeforeReplace(entity);
        }

        protected override async Task AfterReplace(Department entity)
        {
            await base.AfterReplace(entity);
            // 本部门层级路径可能因换父级而变化，同步关系表上的层级快照。
            SyncEmployeeDepartmentHeriarchy(entity.CorpId, entity.Id, entity.HeriarchyId);
            await RefreshDescendantHierarchy(entity);
        }

        protected override Task BeforeDelete(Expression<Func<Department, bool>> filter)
        {
            var deletingDepartments = Repository.Find(new QueryFindOptions<Department> { Filter = filter, Take = int.MaxValue })
                .ToList();
            if (deletingDepartments.Count == 0)
            {
                return base.BeforeDelete(filter);
            }

            var roots = deletingDepartments.Select(x => x.Id).ToList();
            var corpIds = deletingDepartments.Select(x => x.CorpId).Distinct().ToList();
            var protectedDepartmentIds = Repository.Queryable
                .Where(x => corpIds.Contains(x.CorpId))
                .ToList()
                .Where(x => roots.Contains(x.Id)
                    || (x.HeriarchyId != null && roots.Any(root => x.HeriarchyId.Contains($"|{root}|", StringComparison.Ordinal))))
                .Select(x => x.Id)
                .ToList();

            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            var employeeIds = relationRepo.Queryable
                .Where(x => protectedDepartmentIds.Contains(x.DepartmentId))
                .Select(x => x.EmployeeId)
                .Distinct()
                .ToList();
            var employeeRepo = Resolver.GetRepository<Employee>();
            var hasEmployees = employeeRepo.Queryable.Any(x => employeeIds.Contains(x.Id) && !x.DeleteFlag);
            if (hasEmployees)
            {
                throw new BadRequestException("当前部门或下级部门存在员工，不能删除");
            }

            return base.BeforeDelete(filter);
        }

        private void NormalizeHierarchy(Department entity)
        {
            if (string.IsNullOrWhiteSpace(entity.ParentId))
            {
                entity.ParentId = string.Empty;
                entity.ParentName = string.Empty;
                entity.HeriarchyId = $"|{entity.Id}|";
                entity.HeriarchyName = entity.Name;
                return;
            }

            if (entity.ParentId == entity.Id)
            {
                throw new BadRequestException("部门不能设置自身为上级部门");
            }

            var parent = Repository.Get(entity.ParentId);
            if (parent == null || parent.DeleteFlag)
            {
                entity.ParentId = string.Empty;
                entity.ParentName = string.Empty;
                entity.HeriarchyId = $"|{entity.Id}|";
                entity.HeriarchyName = entity.Name;
                return;
            }

            if (!string.Equals(parent.CorpId, entity.CorpId, StringComparison.Ordinal))
            {
                throw new BadRequestException("上级部门必须属于当前企业");
            }

            if (parent.HeriarchyId.Contains($"|{entity.Id}|"))
            {
                throw new BadRequestException("部门不能移动到自己的下级部门下");
            }

            entity.ParentName = parent.Name;
            entity.HeriarchyId = $"{parent.HeriarchyId}{entity.Id}|";
            entity.HeriarchyName = $"{entity.Name}/{parent.HeriarchyName}";
        }

        private async Task RefreshDescendantHierarchy(Department parent)
        {
            var children = Repository.Queryable
                .Where(x => x.CorpId == parent.CorpId && !x.DeleteFlag && x.ParentId == parent.Id)
                .ToList();

            foreach (var child in children)
            {
                child.ParentName = parent.Name;
                child.HeriarchyId = $"{parent.HeriarchyId}{child.Id}|";
                child.HeriarchyName = $"{child.Name}/{parent.HeriarchyName}";
                Repository.Replace(child);
                SyncEmployeeDepartmentHeriarchy(child.CorpId, child.Id, child.HeriarchyId);
                await RefreshDescendantHierarchy(child);
            }
        }

        /// <summary>
        /// 部门层级路径变化时，同步刷新指向该部门的关系表行上的层级快照，
        /// 保证「按部门级联查员工」直接用 <c>EmployeeDepartment.HeriarchyId</c> 的 Contains 即可命中。
        /// </summary>
        private void SyncEmployeeDepartmentHeriarchy(string corpId, string departmentId, string heriarchyId)
        {
            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            var relations = relationRepo.Queryable
                .Where(x => x.CorpId == corpId && x.DepartmentId == departmentId)
                .ToList();

            foreach (var relation in relations)
            {
                relation.HeriarchyId = heriarchyId;
                relationRepo.Replace(relation);
            }
        }

    }
}
