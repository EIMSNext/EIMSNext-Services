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

        private static readonly System.Reflection.MethodInfo StartsWithMethod =
            typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

        public override int Replace(Department entity)
        {
            using var scope = NewTransactionScope();
            var result = base.Replace(entity);
            scope.CommitTransaction();
            return result;
        }

        public override async Task<int> ReplaceAsync(Department entity)
        {
            await using var scope = NewTransactionScope();
            var result = await base.ReplaceAsync(entity).ConfigureAwait(false);
            await scope.CommitTransactionAsync().ConfigureAwait(false);
            return result;
        }

        protected override async Task BeforeAdd(IEnumerable<Department> entities)
        {
            foreach (var entity in entities)
            {
                Repository.EnsureId(entity);

                if (!string.IsNullOrEmpty(entity.ParentId))
                {
                    var parent = await Repository.GetAsync(entity.ParentId);
                    if (parent == null)
                    {
                        entity.ParentId = "";
                        entity.HeriarchyId = $"|{entity.Id}|";
                        entity.HeriarchyName = entity.Name;
                    }
                    else if (!string.Equals(parent.CorpId, entity.CorpId, StringComparison.OrdinalIgnoreCase))
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

            await base.BeforeAdd(entities);
        }

        protected override async Task BeforeReplace(Department entity)
        {
            await NormalizeHierarchy(entity);
            await base.BeforeReplace(entity);
        }

        protected override async Task AfterReplace(Department entity)
        {
            await base.AfterReplace(entity);
            // 本部门层级路径可能因换父级而变化，同步关系表上的层级快照。
            await SyncEmployeeDepartmentHeriarchyAsync(entity.CorpId, entity.Id, entity.HeriarchyId);
            await RefreshDescendantHierarchy(entity);
        }

        protected override async Task BeforeDelete(Expression<Func<Department, bool>> filter)
        {
            var deletingDepartments = await Repository
                .FindAsync(new QueryFindOptions<Department> { Filter = filter, Take = int.MaxValue })
                .ConfigureAwait(false);
            if (deletingDepartments.Count == 0)
            {
                await base.BeforeDelete(filter).ConfigureAwait(false);
                return;
            }

            // 层级路径形如 |a|b|，被删节点及其整棵子树都以它的路径为前缀。
            // 先由部门层级路径定位子树，再按 DepartmentId 检查关系表，避免依赖可能过期的关系快照。
            if (deletingDepartments.Any(x => string.IsNullOrEmpty(x.HeriarchyId)))
            {
                throw new BadRequestException("部门层级数据异常，无法安全删除");
            }

            var prefixes = deletingDepartments
                .Select(x => x.HeriarchyId)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var subTreeDepartmentIds = await Repository
                .FindAsync(BuildSubTreeFilter(prefixes))
                .ConfigureAwait(false);
            var departmentIds = subTreeDepartmentIds
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            var subTreeEmployeeIds = relationRepo.Queryable
                .Where(ed => departmentIds.Contains(ed.DepartmentId))
                .Where(ed => !ed.DeleteFlag)
                .Select(ed => ed.EmployeeId);

            var hasEmployees = await EmployeeRepository
                .AnyAsync(e => !e.DeleteFlag && subTreeEmployeeIds.Contains(e.Id))
                .ConfigureAwait(false);
            if (hasEmployees)
            {
                throw new BadRequestException("当前部门或下级部门存在员工，不能删除");
            }

            await base.BeforeDelete(filter).ConfigureAwait(false);
        }

        /// <summary>
        /// 拼「层级路径以给定前缀之一开头」的谓词：子树判定下推到 SQL（LIKE 'prefix%'），
        /// 扫描量与关系总数无关。
        /// </summary>
        private static Expression<Func<Department, bool>> BuildSubTreeFilter(IReadOnlyList<string> hierarchies)
        {
            var parameter = Expression.Parameter(typeof(Department), "x");
            var property = Expression.Property(parameter, nameof(Department.HeriarchyId));

            Expression? body = null;
            foreach (var hierarchy in hierarchies)
            {
                var startsWith = Expression.Call(property, StartsWithMethod, Expression.Constant(hierarchy, typeof(string)));
                body = body == null ? startsWith : Expression.OrElse(body, startsWith);
            }

            return Expression.Lambda<Func<Department, bool>>(body ?? Expression.Constant(false), parameter);
        }

        private async Task NormalizeHierarchy(Department entity)
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

            var parent = await Repository.GetAsync(entity.ParentId);
            if (parent == null || parent.DeleteFlag)
            {
                entity.ParentId = string.Empty;
                entity.ParentName = string.Empty;
                entity.HeriarchyId = $"|{entity.Id}|";
                entity.HeriarchyName = entity.Name;
                return;
            }

            if (!string.Equals(parent.CorpId, entity.CorpId, StringComparison.OrdinalIgnoreCase))
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

        private async Task RefreshDescendantHierarchy(Department parent, CancellationToken cancellationToken = default)
        {
            var children = await Repository
                .FindAsync(
                    x => x.CorpId == parent.CorpId && !x.DeleteFlag && x.ParentId == parent.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var child in children)
            {
                child.ParentName = parent.Name;
                child.HeriarchyId = $"{parent.HeriarchyId}{child.Id}|";
                child.HeriarchyName = $"{child.Name}/{parent.HeriarchyName}";
                await Repository.ReplaceAsync(child, cancellationToken).ConfigureAwait(false);
                await SyncEmployeeDepartmentHeriarchyAsync(child.CorpId, child.Id, child.HeriarchyId, cancellationToken)
                    .ConfigureAwait(false);
                await RefreshDescendantHierarchy(child, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 部门层级路径变化时，同步刷新指向该部门的关系表行上的层级快照，
        /// 保证「按部门级联查员工」直接用 <c>EmployeeDepartment.HeriarchyId</c> 的 Contains 即可命中。
        /// </summary>
        /// <remarks>
        /// 逐个 Replace 而不合并成一条 UPDATE：层级快照要走仓储的写路径（审计/变更钩子都挂在
        /// SaveChanges 上），ExecuteUpdate 会绕开它们。这里只把同步往返改成异步 ——
        /// 同步阻塞会占住请求线程，部门树越深越容易在突发流量下演变成线程池饥饿。
        /// </remarks>
        private async Task SyncEmployeeDepartmentHeriarchyAsync(
            string corpId,
            string departmentId,
            string heriarchyId,
            CancellationToken cancellationToken = default)
        {
            var relationRepo = Resolver.GetRepository<EmployeeDepartment>();
            var relations = await relationRepo
                .FindAsync(
                    x => x.CorpId == corpId && x.DepartmentId == departmentId,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var relation in relations)
            {
                relation.HeriarchyId = heriarchyId;
                await relationRepo.ReplaceAsync(relation, cancellationToken).ConfigureAwait(false);
            }
        }

    }
}
