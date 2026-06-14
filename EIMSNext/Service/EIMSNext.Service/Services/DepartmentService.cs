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
    }
}
