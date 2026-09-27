using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	/// <summary>
	/// 打印定义的 API 服务。
	/// </summary>
	/// <param name="resolver">服务解析器。</param>
	public class PrintDefApiService(IResolver resolver) : ApiServiceBase<PrintDef, PrintDefViewModel, IPrintDefService>(resolver)
	{
        /// <summary>
        /// 新增实体核心逻辑。
        /// </summary>
        protected override Task AddAsyncCore(PrintDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.AddAsyncCore(entity);
        }

        /// <summary>
        /// 更新实体核心逻辑。
        /// </summary>
        protected override Task<ReplaceOneResult> ReplaceAsyncCore(PrintDef entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.ReplaceAsyncCore(entity);
        }

        /// <summary>
        /// 删除实体核心逻辑。
        /// </summary>
        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var printDefs = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (printDefs.Count != idList.Count)
            {
                throw new BadRequestException("打印定义不存在");
            }

            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            foreach (var printDef in printDefs)
            {
                evaluator.EnsureCanManageApp(printDef.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
