using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.OData.Query;
using EIMSNext.Core;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class AuthGroupController(IResolver resolver) : ODataController<AuthGroupApiService, AuthGroup, AuthGroupViewModel, AuthGroupRequest>(resolver)
	{
        protected override IQueryable<AuthGroupViewModel> FilterByPermission(IQueryable<AuthGroupViewModel> query, ODataQueryOptions<AuthGroupViewModel> options)
        {
            if (IdentityType.App_Admins.HasFlag(IdentityContext.IdentityType))
            {
                return base.FilterByPermission(query, options);
            }
            else if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                var emp = (IdentityContext.CurrentEmployee as Employee)!;

                //TODO: 性能不一定好，先这样写
                var empId = emp.Id;
                var roleIds = emp.Roles.Select(x => x.RoleId).ToList();
                var deptIds = Resolver.GetRepository<EmployeeDepartment>().Queryable
                    .Where(x => x.CorpId == IdentityContext.CurrentCorpId && x.EmployeeId == empId)
                    .Select(x => x.DepartmentId)
                    .ToList();
                var departments = Resolver.GetService<Department>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag)
                    .Select(x => new { x.Id, x.HeriarchyId })
                    .ToList();
                var employeeDepartmentHierarchies = departments
                    .Where(x => deptIds.Contains(x.Id))
                    .Select(x => x.HeriarchyId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                var pDeptIds = departments
                    .Where(x => employeeDepartmentHierarchies.Any(heriarchyId => heriarchyId.Contains($"|{x.Id}|")))
                    .Select(x => x.Id)
                    .ToList();

                return query.Where(x => x.Members.Any(m => (m.Type == MemberType.Employee && m.Id == empId) || (m.Type == MemberType.Role && roleIds.Contains(m.Id)) || (m.Type == MemberType.Department && (m.CascadedDept && pDeptIds.Contains(m.Id) || deptIds.Contains(m.Id)))));
            }

            return query.Where(x => false);
        }
    }
}
