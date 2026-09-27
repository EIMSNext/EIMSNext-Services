using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.UriParser;
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
    public class EmployeeGroupController(IResolver resolver) : ODataController<EmployeeGroupApiService, EmployeeGroup, EmployeeGroupViewModel, EmployeeGroupRequest>(resolver)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected override IQueryable<EmployeeGroupViewModel> Expand(IQueryable<EmployeeGroupViewModel> query, ODataQueryOptions<EmployeeGroupViewModel> options)
        {
            var expands = options.SelectExpand?.SelectExpandClause?.SelectedItems?.Where(x => x is ExpandedNavigationSelectItem);

            if (expands != null)
            {
                foreach (ExpandedNavigationSelectItem item in expands)
                {
                    if (item.NavigationSource.Name.Equals("employeeGroupCategory", StringComparison.OrdinalIgnoreCase))
                    {
                        var groups = Resolver.GetService<EmployeeGroupCategory>().All();
                        query = query.Join(groups, x => x.EmployeeGroupCategoryId, y => y.Id, ObjectConvert.ProjExp<EmployeeGroupViewModel, EmployeeGroupCategory>(x => x.EmployeeGroupCategory!));
                    }
                }
            }

            return base.Expand(query, options);
        }

        protected override IQueryable<EmployeeGroupViewModel> FilterByPermission(IQueryable<EmployeeGroupViewModel> query, ODataQueryOptions<EmployeeGroupViewModel> options)
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

            var ids = snapshot.ContactViewEmployeeGroupIds;
            return ids.Count == 0 ? query.Where(x => false) : query.Where(x => ids.Contains(x.Id));
        }

        private bool IsAdminScope()
        {
            return Request.Query.TryGetValue("adminScope", out var value) &&
                value.FirstOrDefault().EqualsIgnoreCase("true");
        }
    }
}
