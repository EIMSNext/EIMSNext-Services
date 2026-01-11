using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Core;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.ServiceApi.ODataControllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class AppController(IResolver resolver) : ODataController<AppApiService, App, AppViewModel, AppRequest>(resolver)
    {
        protected override IQueryable<AppViewModel> FilterByPermission(IQueryable<AppViewModel> query, ODataQueryOptions<AppViewModel> options)
        {
            if (IdentityType.App_Admins.HasFlag(IdentityContext.IdentityType))
            {
                return base.FilterByPermission(query,options);
            }
            else if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                var emp = (IdentityContext.CurrentEmployee as Employee)!;

                //TODO: 性能不一定好，先这样写
                var empId = emp.Id;
                var roleIds = emp.Roles.Select(x => x.RoleId).ToList();
                var deptId = emp.DepartmentId;
                var pDeptIds = Resolver.GetService<Department>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.HeriarchyId.Contains($"|{deptId}|")).Select(x => x.Id).ToList();

                var appIds = Resolver.GetService<AuthGroup>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.Members.Any(m => (m.Type == MemberType.Employee && m.Id == empId) || (m.Type == MemberType.Role && roleIds.Contains(m.Id)) || (m.Type == MemberType.Department && (m.CascadedDept && pDeptIds.Contains(m.Id) || deptId == m.Id)))).Select(x => x.AppId).Distinct().ToList();

                return query.Where(x => appIds.Contains(x.Id));
            }

            return query.Where(x => false);
        }
    }
}
