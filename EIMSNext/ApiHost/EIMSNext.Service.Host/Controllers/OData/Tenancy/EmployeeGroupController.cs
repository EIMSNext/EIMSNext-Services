using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using Microsoft.AspNetCore.OData.Query;
using EIMSNext.ApiService;
using EIMSNext.Service.Host.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using EIMSNext.Common.Extensions;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class EmployeeGroupController(IResolver resolver) : ODataController<EmployeeGroupApiService, EmployeeGroup, EmployeeGroupRequest>(resolver)
    {
        protected override IQueryable<EmployeeGroup> FilterByPermission(IQueryable<EmployeeGroup> query, ODataQueryOptions<EmployeeGroup> options)
        {
            query = base.FilterByPermission(query, options);
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

            var ids = snapshot.ContactViewEmployeeGroupIds;
            return ids.Count == 0 ? query.Where(x => false) : query.Where(x => ids.Contains(x.Id));
        }

    }
}

