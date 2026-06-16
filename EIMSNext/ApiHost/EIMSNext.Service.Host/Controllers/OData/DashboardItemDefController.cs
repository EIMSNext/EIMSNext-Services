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
	public class DashboardItemDefController(IResolver resolver) : ODataController<DashboardItemDefApiService, DashboardItemDef, DashboardItemDefViewModel, DashboardItemDefRequest>(resolver)
	{
        protected override IQueryable<DashboardItemDefViewModel> FilterByPermission(IQueryable<DashboardItemDefViewModel> query, ODataQueryOptions<DashboardItemDefViewModel> options)
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
                return query.Where(x => dashboardIds.Contains(x.DashboardId));
            }

            if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                var dashboardIds = evaluator.GetUsageDashboardIdsForCurrentEmployee(QueryAppId);
                return query.Where(x => dashboardIds.Contains(x.DashboardId));
            }

            return query.Where(x => false);
        }
	}
}
