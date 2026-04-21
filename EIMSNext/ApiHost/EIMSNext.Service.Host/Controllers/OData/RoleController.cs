using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;
using EIMSNext.Common;
using EIMSNext.Core;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.UriParser;
using EIMSNext.ApiService;
using EIMSNext.Service.Host.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class RoleController(IResolver resolver) : ODataController<RoleApiService, Role, RoleViewModel, RoleRequest>(resolver)
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected override IQueryable<RoleViewModel> Expand(IQueryable<RoleViewModel> query, ODataQueryOptions<RoleViewModel> options)
        {
            var expands = options.SelectExpand?.SelectExpandClause?.SelectedItems?.Where(x => x is ExpandedNavigationSelectItem);

            if (expands != null)
            {
                foreach (ExpandedNavigationSelectItem item in expands)
                {
                    if (item.NavigationSource.Name.Equals("rolegroup", StringComparison.OrdinalIgnoreCase))
                    {
                        var groups = Resolver.GetService<RoleGroup>().All();
                        query = query.Join(groups, x => x.RoleGroupId, y => y.Id, ObjectConvert.ProjExp<RoleViewModel, RoleGroup>(x => x.RoleGroup!));
                    }
                }
            }

            return base.Expand(query, options);
        }
    }
}
