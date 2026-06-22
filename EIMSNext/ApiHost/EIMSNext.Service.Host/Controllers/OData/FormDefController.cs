using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Core;
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
    public class FormDefController(IResolver resolver) : ODataController<FormDefApiService, FormDef, FormDefViewModel, FormDefRequest>(resolver)
    {
        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        public override IActionResult Get(ODataQueryOptions<FormDefViewModel> options)
        {
            return base.Get(options);
        }

        [IdentityType(IdentityTypeDefaults.PublicBusinessUser)]
        [PublicScope(PublicScope.DashLink)]
        public override Microsoft.AspNetCore.OData.Results.SingleResult Get([Microsoft.AspNetCore.OData.Formatter.FromODataUri] string key, ODataQueryOptions<FormDefViewModel> options)
        {
            if (IdentityContext.IdentityType == IdentityType.Public && !Resolver.Resolve<IPublicAccessValidator>().CanReadFormDefinition(key))
            {
                return Microsoft.AspNetCore.OData.Results.SingleResult.Create(Enumerable.Empty<FormDefViewModel>().AsQueryable());
            }

            return base.Get(key, options);
        }

        protected override IQueryable<FormDefViewModel> FilterByPermission(IQueryable<FormDefViewModel> query, ODataQueryOptions<FormDefViewModel> options)
        {
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return base.FilterByPermission(query, options);
            }

            if (IdentityContext.IdentityType == IdentityType.Public)
            {
                var validator = Resolver.Resolve<IPublicAccessValidator>();
                var formIds = validator.GetReadableFormIds().ToList();
                return formIds.Count == 0 ? query.Where(x => false) : query.Where(x => formIds.Contains(x.Id));
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                query = base.FilterByPermission(query, options);
                string? appId = QueryAppId;
                var formIds = evaluator.GetUsageFormIdsForCurrentEmployee(appId);
                var manageableAppIds = evaluator.GetSnapshot().ManageableAppIds;

                return query.Where(x =>
                    formIds.Contains(x.Id) ||
                    (manageableAppIds.Contains(x.AppId) && (string.IsNullOrEmpty(appId) || x.AppId == appId)));
            }

            if (IdentityType.Employee_Admins.HasFlag(IdentityContext.IdentityType))
            {
                query = base.FilterByPermission(query, options);
                string? appId = QueryAppId;
                var formIds = evaluator.GetUsageFormIdsForCurrentEmployee(appId);
                return query.Where(x => formIds.Contains(x.Id));
            }

            return query.Where(x => false);
        }
    }
}
