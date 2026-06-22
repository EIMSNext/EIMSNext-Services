using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using EIMSNext.Service.Host.Authorization;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    [IdentityType(IdentityTypeDefaults.BusinessUser)]
	public class DashboardItemDefController(IResolver resolver) : ODataController<DashboardItemDefApiService, DashboardItemDef, DashboardItemDefViewModel, DashboardItemDefRequest>(resolver)
	{
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        public override IActionResult Get(ODataQueryOptions<DashboardItemDefViewModel> options)
        {
            return base.Get(options);
        }

        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        public override Microsoft.AspNetCore.OData.Results.SingleResult Get([Microsoft.AspNetCore.OData.Formatter.FromODataUri] string key, ODataQueryOptions<DashboardItemDefViewModel> options)
        {
            if (IdentityContext.IdentityType == IdentityType.Public && !Resolver.Resolve<IPublicAccessValidator>().CanReadDashboardItem(key))
            {
                return Microsoft.AspNetCore.OData.Results.SingleResult.Create(Enumerable.Empty<DashboardItemDefViewModel>().AsQueryable());
            }

            return base.Get(key, options);
        }

        protected override IQueryable<DashboardItemDefViewModel> FilterByPermission(IQueryable<DashboardItemDefViewModel> query, ODataQueryOptions<DashboardItemDefViewModel> options)
        {
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return base.FilterByPermission(query, options);
            }

            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                return validator.IsAnySectionEnabled()
                    ? query.Where(x => x.DashboardId == validator.TargetId)
                    : query.Where(x => false);
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
