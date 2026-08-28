using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using Microsoft.AspNetCore.OData.Query;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class FormDataPermissionGroupController(IResolver resolver) : ODataController<FormDataPermissionGroupApiService, FormDataPermissionGroup, FormDataPermissionGroupViewModel, FormDataPermissionGroupRequest>(resolver)
	{
        protected override IQueryable<FormDataPermissionGroupViewModel> FilterByPermission(IQueryable<FormDataPermissionGroupViewModel> query, ODataQueryOptions<FormDataPermissionGroupViewModel> options)
        {
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return base.FilterByPermission(query, options);
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                query = base.FilterByPermission(query, options);
                var manageableAppIds = evaluator.GetSnapshot().ManageableAppIds;
                var formIds = evaluator.GetUsageFormIdsForCurrentEmployee(QueryAppId);
                return query.Where(x => manageableAppIds.Contains(x.AppId) || formIds.Contains(x.FormId));
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
