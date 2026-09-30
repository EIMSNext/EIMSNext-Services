using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;

namespace EIMSNext.ApiService
{
	/// <summary>
	/// 仪表盘定义的 API 服务。
	/// </summary>
	/// <param name="resolver">服务解析器。</param>
	public class DashboardDefApiService(IResolver resolver) : ApiServiceBase<DashboardDef, DashboardDefViewModel, IDashboardDefService>(resolver)
	{
        private const int MaxNameLength = 100;

        /// <summary>
        /// 新增实体核心逻辑。
        /// </summary>
        protected override Task AddAsyncCore(DashboardDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            ValidateName(entity.Name);
            return base.AddAsyncCore(entity);
        }

        /// <summary>
        /// 更新实体核心逻辑。
        /// </summary>
        protected override Task<int> ReplaceAsyncCore(DashboardDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            ValidateName(entity.Name);
            return base.ReplaceAsyncCore(entity);
        }

        private static void ValidateName(string name)
        {
            if (!string.IsNullOrEmpty(name) && name.Length > MaxNameLength)
            {
                throw new BadRequestException($"仪表盘名称长度不能超过 {MaxNameLength} 个字符");
            }
        }

        /// <summary>
        /// 删除实体核心逻辑。
        /// </summary>
        protected override async Task<int> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var dashboards = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (dashboards.Count != idList.Count)
            {
                throw new BadRequestException("仪表盘不存在");
            }

            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            foreach (var dashboard in dashboards)
            {
                evaluator.EnsureCanManageApp(dashboard.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
