using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class DashboardDefApiService(IResolver resolver) : ApiServiceBase<DashboardDef, DashboardDefViewModel, IDashboardDefService>(resolver)
	{
        protected override Task AddAsyncCore(DashboardDef entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(DashboardDef entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var dashboards = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (dashboards.Count != idList.Count)
            {
                throw new BadRequestException("仪表盘不存在");
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var dashboard in dashboards)
            {
                evaluator.EnsureCanManageApp(dashboard.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
