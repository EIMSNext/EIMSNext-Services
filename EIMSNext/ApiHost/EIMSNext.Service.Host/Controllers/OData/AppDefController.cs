using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
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
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return base.FilterByPermission(query, options);
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                query = base.FilterByPermission(query, options);
                var appIds = evaluator.GetUsageAppIdsForCurrentEmployee()
                    .Concat(evaluator.GetSnapshot().ManageableAppIds)
                    .Distinct()
                    .ToList();
                return query.Where(x => appIds.Contains(x.Id));
            }

            if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                var appIds = evaluator.GetUsageAppIdsForCurrentEmployee();
                return query.Where(x => appIds.Contains(x.Id));
            }

            return query.Where(x => false);
        }
    }
}
