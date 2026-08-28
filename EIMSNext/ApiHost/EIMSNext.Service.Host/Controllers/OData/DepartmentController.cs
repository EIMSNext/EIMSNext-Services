using Asp.Versioning;

using HKH.Mef2.Integration;
using EIMSNext.Service.Host.OData;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Entities;
using Microsoft.AspNetCore.Mvc;
using EIMSNext.Service.Host.Authorization;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Deltas;
using EIMSNext.Service.Host.Requests;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resolver"></param>
    [ApiVersion(1.0)]
    public class DepartmentController(IResolver resolver) : ODataController<DepartmentApiService, Department, DepartmentViewModel, DepartmentRequest>(resolver)
    {
        protected override IQueryable<DepartmentViewModel> FilterByPermission(IQueryable<DepartmentViewModel> query, ODataQueryOptions<DepartmentViewModel> options)
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
            if (snapshot.ContactViewDepartmentScopeMode == AdminPermissionSnapshot.ToWireScopeMode(ScopeMode.All))
            {
                return query;
            }

            var ids = snapshot.ContactViewDepartmentIds;
            return ids.Count == 0 ? query.Where(x => false) : query.Where(x => ids.Contains(x.Id));
        }

        [IdentityType(IdentityType.Corp_Admins | IdentityType.System | IdentityType.Client)]
        public override Task<ActionResult> Post([FromBody] DepartmentRequest model)
        {
            return base.Post(model);
        }

        [IdentityType(IdentityType.Corp_Admins | IdentityType.System | IdentityType.Client)]
        public override Task<ActionResult> Put([FromODataUri] string key, [FromBody] DepartmentRequest model)
        {
            return base.Put(key, model);
        }

        [IdentityType(IdentityType.Corp_Admins | IdentityType.System | IdentityType.Client)]
        public override Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<DepartmentRequest> delta)
        {
            return base.Patch(key, delta);
        }

        [IdentityType(IdentityType.Corp_Admins | IdentityType.System | IdentityType.Client)]
        public override Task<ActionResult> Patch([FromBody] DeltaSet<DepartmentRequest> deltas)
        {
            return base.Patch(deltas);
        }

        [IdentityType(IdentityType.Corp_Admins | IdentityType.System | IdentityType.Client)]
        public override Task<ActionResult> Delete([FromODataUri] string key, [FromBody] DeleteBatch? batch)
        {
            return base.Delete(key, batch);
        }

        private bool IsAdminScope()
        {
            return Request.Query.TryGetValue("adminScope", out var value) &&
                string.Equals(value.FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
