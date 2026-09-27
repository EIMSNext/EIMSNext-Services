using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
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
using EIMSNext.Entities;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class EmployeeGroupCategoryController(IResolver resolver) : ODataController<EmployeeGroupCategoryApiService, EmployeeGroupCategory, EmployeeGroupCategoryViewModel, EmployeeGroupCategoryRequest>(resolver)
	{
        protected override IQueryable<EmployeeGroupCategoryViewModel> FilterByPermission(IQueryable<EmployeeGroupCategoryViewModel> query, ODataQueryOptions<EmployeeGroupCategoryViewModel> options)
        {
            query = base.FilterByPermission(query, options);
            if (!IsAdminScope())
            {
                return query;
            }

            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            if (!evaluator.ShouldApplyNormalAdminRules)
            {
                return query;
            }

            var snapshot = evaluator.GetSnapshot();
            if (snapshot.ContactViewEmployeeGroupScopeMode == AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All))
            {
                return query;
            }

            var employeeGroupIds = snapshot.ContactViewEmployeeGroupIds;
            if (employeeGroupIds.Count == 0)
            {
                return query.Where(x => false);
            }

            var groupIds = Resolver.GetService<EmployeeGroup>().All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    employeeGroupIds.Contains(x.Id) &&
                    !string.IsNullOrEmpty(x.EmployeeGroupCategoryId))
                .Select(x => x.EmployeeGroupCategoryId)
                .Distinct()
                .ToList();

            return groupIds.Count == 0 ? query.Where(x => false) : query.Where(x => groupIds.Contains(x.Id));
        }

        private bool IsAdminScope()
        {
            return Request.Query.TryGetValue("adminScope", out var value) &&
                string.Equals(value.FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
        }
	}
}
