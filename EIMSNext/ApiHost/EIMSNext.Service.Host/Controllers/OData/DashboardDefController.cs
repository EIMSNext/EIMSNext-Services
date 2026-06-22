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
	public class DashboardDefController(IResolver resolver) : ODataController<DashboardDefApiService, DashboardDef, DashboardDefViewModel, DashboardDefRequest>(resolver)
	{
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        public override IActionResult Get(ODataQueryOptions<DashboardDefViewModel> options)
        {
            return base.Get(options);
        }

        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        public override Microsoft.AspNetCore.OData.Results.SingleResult Get([Microsoft.AspNetCore.OData.Formatter.FromODataUri] string key, ODataQueryOptions<DashboardDefViewModel> options)
        {
            if (IdentityContext.IdentityType == IdentityType.Public && !Resolver.Resolve<IPublicAccessValidator>().CanReadDashboard(key))
            {
                return Microsoft.AspNetCore.OData.Results.SingleResult.Create(Enumerable.Empty<DashboardDefViewModel>().AsQueryable());
            }

            return base.Get(key, options);
        }

        protected override IQueryable<DashboardDefViewModel> FilterByPermission(IQueryable<DashboardDefViewModel> query, ODataQueryOptions<DashboardDefViewModel> options)
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
                    ? query.Where(x => x.Id == validator.TargetId)
                    : query.Where(x => false);
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
