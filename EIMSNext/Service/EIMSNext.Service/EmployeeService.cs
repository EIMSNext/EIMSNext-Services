using HKH.Mef2.Integration;
using EIMSNext.Core.Service;
using EIMSNext.Entity;
using EIMSNext.Service.Interface;
using MongoDB.Driver;

namespace EIMSNext.Service
{
    public class EmployeeService(IResolver resolver) : EntityServiceBase<Employee>(resolver), IEmployeeService
    {
        public Task<UpdateResult> AddToRoleAsync(Role role, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.AddToSet(x => x.Roles, new EmpRole { RoleId = role.Id, RoleName = role.Name });
            var filter = FilterBuilder.And(FilterBuilder.In(x => x.Id, empIds),
                FilterBuilder.Not(FilterBuilder.ElemMatch(x => x.Roles, r => r.RoleId == role.Id) // 排除已存在该RoleId的员工
    )           );

            return Repository.UpdateManyAsync(filter, update);
        }

        public Task<UpdateResult> RemoveFromRoleAsync(string roleId, IEnumerable<string> empIds)
        {
            var update = UpdateBuilder.PullFilter(x => x.Roles, r => r.RoleId == roleId);
            var filter = FilterBuilder.In(x => x.Id, empIds);

            return Repository.UpdateManyAsync(filter, update);
        }
    }
}
