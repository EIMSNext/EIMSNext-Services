using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.UriParser;
using MongoDB.Driver.Linq;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class EmployeeController(IResolver resolver) : ODataController<EmployeeApiService, Employee, EmployeeViewModel, EmployeeRequest>(resolver)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected override IQueryable<EmployeeViewModel> Expand(IQueryable<EmployeeViewModel> query, ODataQueryOptions<EmployeeViewModel> options)
        {
            var expands = options.SelectExpand?.SelectExpandClause?.SelectedItems?.Where(x => x is ExpandedNavigationSelectItem);

            if (expands != null)
            {
                foreach (ExpandedNavigationSelectItem item in expands)
                {
                    if (item.NavigationSource.Name.Equals("department", StringComparison.OrdinalIgnoreCase))
                    {
                        var deparments = Resolver.GetService<Department>().All();
                        query = query.Join(deparments, x => x.DepartmentId, y => y.Id, ObjectConvert.ProjExp<EmployeeViewModel, Department>(x => x.Department!));
                    }
                }
            }

            return base.Expand(query, options);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected override IQueryable<EmployeeViewModel> FilterResult(IQueryable<EmployeeViewModel> query, ODataQueryOptions<EmployeeViewModel> options)
        {
            query = base.FilterResult(query, options);
            query = query.Where(x => !x.IsDummy);

            if (IsAdminScope())
            {
                var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
                if (evaluator.ShouldApplyNormalAdminRules)
                {
                    var snapshot = evaluator.GetSnapshot();
                    if (snapshot.ContactViewDepartmentScopeMode != AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All))
                    {
                        var ids = snapshot.ContactViewDepartmentIds;
                        query = ids.Count == 0 ? query.Where(x => false) : query.Where(x => ids.Contains(x.DepartmentId));
                    }
                }
            }

            return query;
        }

        private bool IsAdminScope()
        {
            return Request.Query.TryGetValue("adminScope", out var value) &&
                string.Equals(value.FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
