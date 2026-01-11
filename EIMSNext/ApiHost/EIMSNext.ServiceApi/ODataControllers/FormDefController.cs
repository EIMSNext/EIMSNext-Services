using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.ServiceApi.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Entity;
using EIMSNext.Core;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.ServiceApi.ODataControllers
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class FormDefController(IResolver resolver) : ODataController<FormDefApiService, FormDef, FormDefViewModel, FormDefRequest>(resolver)
    {
        protected override IQueryable<FormDefViewModel> FilterByPermission(IQueryable<FormDefViewModel> query, ODataQueryOptions<FormDefViewModel> options)
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
                var deptId = emp.DepartmentId;
                var pDeptIds = Resolver.GetService<Department>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && x.HeriarchyId.Contains($"|{deptId}|")).Select(x => x.Id).ToList();

                string? appId = QueryAppId;
                var formIds = Resolver.GetService<AuthGroup>().Query(x => x.CorpId == IdentityContext.CurrentCorpId && (string.IsNullOrEmpty(appId) || x.AppId == appId) && x.Members.Any(m => (m.Type == MemberType.Employee && m.Id == empId) || (m.Type == MemberType.Role && roleIds.Contains(m.Id)) || (m.Type == MemberType.Department && (m.CascadedDept && pDeptIds.Contains(m.Id) || deptId == m.Id)))).Select(x => x.FormId).Distinct().ToList();

                return query.Where(x => formIds.Contains(x.Id));
            }

            return query.Where(x => false);
        }
    }
}
