using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModel;
using EIMSNext.ApiService.ViewModel;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Entity;
using EIMSNext.ServiceApi.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.UriParser;
using MongoDB.Driver.Linq;

namespace EIMSNext.ServiceApi.ODataControllers
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

            return query;
        }
    }
}
