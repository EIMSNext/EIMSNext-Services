using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    public class FormListViewController(IResolver resolver) : ODataController<FormListViewApiService, FormListView, FormListViewViewModel, FormListViewRequest>(resolver)
    {
        protected override IQueryable<FormListViewViewModel> FilterByPermission(IQueryable<FormListViewViewModel> query, ODataQueryOptions<FormListViewViewModel> options)
        {
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return base.FilterByPermission(query, options);
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                query = base.FilterByPermission(query, options);
                var formIds = evaluator.GetUsageFormIdsForCurrentEmployee(QueryAppId);
                var manageableAppIds = evaluator.GetSnapshot().ManageableAppIds;
                return query.Where(x => formIds.Contains(x.FormId) || manageableAppIds.Contains(x.AppId));
            }

            if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                var formIds = evaluator.GetUsageFormIdsForCurrentEmployee(QueryAppId);
                return query.Where(x => formIds.Contains(x.FormId));
            }

            return query.Where(x => false);
        }
    }
}
