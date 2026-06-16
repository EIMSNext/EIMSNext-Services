using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class DashboardDefController(IResolver resolver) : ODataController<DashboardDefApiService, DashboardDef, DashboardDefViewModel, DashboardDefRequest>(resolver)
	{
        protected override IQueryable<DashboardDefViewModel> FilterByPermission(IQueryable<DashboardDefViewModel> query, ODataQueryOptions<DashboardDefViewModel> options)
        {
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return base.FilterByPermission(query, options);
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                query = base.FilterByPermission(query, options);
                var dashboardIds = evaluator.GetUsageDashboardIdsForCurrentEmployee(QueryAppId);
                return query.Where(x => dashboardIds.Contains(x.Id));
            }

            if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                var dashboardIds = evaluator.GetUsageDashboardIdsForCurrentEmployee(QueryAppId);
                return query.Where(x => dashboardIds.Contains(x.Id));
            }

            return query.Where(x => false);
        }
	}
}
