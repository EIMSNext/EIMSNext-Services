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
                var deptId = emp.DepartmentId;
                var pDeptIds = Resolver.GetService<Department>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.HeriarchyId.Contains($"|{deptId}|")) .Select(x => x.Id).ToList();

                var appIds = Resolver.GetService<AuthGroup>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.Members.Any(m => (m.Type == MemberType.Employee && m.Id == empId) || (m.Type == MemberType.Role && roleIds.Contains(m.Id)) || (m.Type == MemberType.Department && (m.CascadedDept && pDeptIds.Contains(m.Id) || deptId == m.Id)))).Select(x => x.AppId).Distinct().ToList();

                return query.Where(x => appIds.Contains(x.Id));
            }

            return query.Where(x => false);
        }
    }
}
