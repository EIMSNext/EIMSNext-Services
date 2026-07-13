using HKH.Mef2.Integration;
using EIMSNext.Core;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.Common;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class DepartmentService(IResolver resolver) : EntityServiceBase<Department>(resolver), IDepartmentService
    {
        private IRepository<Employee> EmployeeRepository => Resolver.GetRepository<Employee>();

        protected override Task BeforeAdd(IEnumerable<Department> entities, IClientSessionHandle? session)
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

            return base.BeforeAdd(entities, session);
        }

        protected override Task BeforeReplace(Department entity, IClientSessionHandle? session)
        {
            NormalizeHierarchy(entity);
            return base.BeforeReplace(entity, session);
        }

        protected override async Task AfterReplace(Department entity, IClientSessionHandle? session)
        {
            await base.AfterReplace(entity, session);
            await RefreshDescendantHierarchy(entity, session);
            await UpdateEmployeeEmpDeptsOnNameChangeAsync(entity.Id, entity.Name);
        }

        protected override Task BeforeDelete(FilterDefinition<Department> filter, IClientSessionHandle? session)
        {
            var deletingDepartments = Repository.Find(new MongoFindOptions<Department> { Filter = filter }, session).ToList();
            if (deletingDepartments.Count == 0)
            {
                return base.BeforeDelete(filter, session);
            }

            var roots = deletingDepartments.Select(x => x.Id).ToList();
            var corpIds = deletingDepartments.Select(x => x.CorpId).Distinct().ToList();
            var protectedDepartmentIds = Repository.Queryable
                .Where(x => corpIds.Contains(x.CorpId))
                .ToList()
                .Where(x => roots.Contains(x.Id) || roots.Any(root => x.HeriarchyId.Contains($"|{root}|")))
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

            return base.BeforeDelete(filter, session);
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

            if (parent.HeriarchyId.Contains($"|{entity.Id}|"))
            {
                throw new BadRequestException("部门不能移动到自己的下级部门下");
            }

            entity.ParentName = parent.Name;
            entity.HeriarchyId = $"{parent.HeriarchyId}{entity.Id}|";
            entity.HeriarchyName = $"{entity.Name}/{parent.HeriarchyName}";
        }

        private async Task RefreshDescendantHierarchy(Department parent, IClientSessionHandle? session)
        {
            var children = Repository.Queryable
                .Where(x => x.CorpId == parent.CorpId && !x.DeleteFlag && x.ParentId == parent.Id)
                .ToList();

            foreach (var child in children)
            {
                child.ParentName = parent.Name;
                child.HeriarchyId = $"{parent.HeriarchyId}{child.Id}|";
                child.HeriarchyName = $"{child.Name}/{parent.HeriarchyName}";
                Repository.Replace(child, session);
                await UpdateEmployeeEmpDeptsOnHierarchyChangeAsync(child.Id, child.HeriarchyId);
                await RefreshDescendantHierarchy(child, session);
            }
        }

        private async Task UpdateEmployeeEmpDeptsOnHierarchyChangeAsync(string departmentId, string newHeriarchyId)
        {
            var filter = Builders<Employee>.Filter.ElemMatch(
                x => x.EmpDepts,
                d => d.Id == departmentId);
            var update = Builders<Employee>.Update.Set(
                "EmpDepts.$.HeriarchyId", newHeriarchyId);
            await EmployeeRepository.UpdateManyAsync(filter, update);
        }

        private async Task UpdateEmployeeEmpDeptsOnNameChangeAsync(string departmentId, string newName)
        {
            var filter = Builders<Employee>.Filter.ElemMatch(
                x => x.EmpDepts,
                d => d.Id == departmentId);
            var update = Builders<Employee>.Update.Set(
                "EmpDepts.$.Name", newName);
            await EmployeeRepository.UpdateManyAsync(filter, update);
        }
    }
}
