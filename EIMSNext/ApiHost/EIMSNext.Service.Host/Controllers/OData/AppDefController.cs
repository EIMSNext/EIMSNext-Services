using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    public class AppDefController(IResolver resolver) : ODataController<AppDefApiService, AppDef, AppDefViewModel, AppRequest>(resolver)
    {
        protected override IQueryable<AppDefViewModel> FilterByPermission(IQueryable<AppDefViewModel> query, ODataQueryOptions<AppDefViewModel> options)
        {
            if (IdentityType.App_Admins.HasFlag(IdentityContext.IdentityType))
            {
                return base.FilterByPermission(query, options);
            }
            else if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                var emp = (IdentityContext.CurrentEmployee as Employee)!;

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

                var appIds = Resolver.GetService<AuthGroup>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.Members.Any(m => (m.Type == MemberType.Employee && m.Id == empId) || (m.Type == MemberType.Role && roleIds.Contains(m.Id)) || (m.Type == MemberType.Department && (m.CascadedDept && pDeptIds.Contains(m.Id) || deptIds.Contains(m.Id))))).Select(x => x.AppId).Distinct().ToList();

                return query.Where(x => appIds.Contains(x.Id));
            }

            return query.Where(x => false);
        }
    }
}
