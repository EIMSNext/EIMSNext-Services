using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
	public class RoleGroupController(IResolver resolver) : ODataController<RoleGroupApiService, RoleGroup, RoleGroupViewModel, RoleGroupRequest>(resolver)
	{
        protected override IQueryable<RoleGroupViewModel> FilterByPermission(IQueryable<RoleGroupViewModel> query, ODataQueryOptions<RoleGroupViewModel> options)
        {
            query = base.FilterByPermission(query, options);
            if (!IsAdminScope())
            {
                return query;
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (!evaluator.ShouldApplyNormalAdminRules)
            {
                return query;
            }

            var snapshot = evaluator.GetSnapshot();
            if (snapshot.ContactViewRoleScopeMode == AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All))
            {
                return query;
            }

            var roleIds = snapshot.ContactViewRoleIds;
            if (roleIds.Count == 0)
            {
                return query.Where(x => false);
            }

            var groupIds = Resolver.GetService<Role>().All()
                .Where(x =>
                    x.CorpId == IdentityContext.CurrentCorpId &&
                    !x.DeleteFlag &&
                    roleIds.Contains(x.Id) &&
                    !string.IsNullOrEmpty(x.RoleGroupId))
                .Select(x => x.RoleGroupId)
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
