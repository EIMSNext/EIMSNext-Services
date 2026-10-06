using Asp.Versioning;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.Service.Host.Authorization;
using EIMSNext.Service.Host.OData;
using HKH.Mef2.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace EIMSNext.Service.Host.Controllers.OData
{
    [ApiVersion(1.0)]
    public class AppDefController(IResolver resolver) : ODataController<AppDefApiService, AppDef, AppRequest>(resolver)
    {
        /// <summary>
        /// 查询应用。平台管理员需跨企业检索应用，故放开身份限制（基类仅放行 BusinessUser）。
        /// </summary>
        /// <remarks>
        /// 仅放行查询，不放开 Post/Put/Patch/Delete：写操作仍走 <see cref="TenantAccessEvaluator"/> 的
        /// EnsureCanCreateApp / EnsureCanManageApp 校验。
        /// </remarks>
        [IdentityType(IdentityTypeDefaults.Authenticated)]
        public override IActionResult Get(ODataQueryOptions<AppDef> options)
        {
            return base.Get(options);
        }

        protected override IQueryable<AppDef> FilterByPermission(IQueryable<AppDef> query, ODataQueryOptions<AppDef> options)
        {
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();

            // 平台管理员需要跨企业检索应用，不做企业隔离，也不用可管理应用集合收窄。
            if (IdentityContext.IdentityType == IdentityType.PlatAdmin)
            {
                return query;
            }

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

