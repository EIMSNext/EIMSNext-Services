using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	/// <summary>
	/// 表单通知的 API 服务。
	/// </summary>
	/// <param name="resolver">服务解析器。</param>
	public class FormNotifyApiService(IResolver resolver) : ApiServiceBase<FormNotify, FormNotifyViewModel, IFormNotifyService>(resolver)
	{
        /// <summary>
        /// 新增实体核心逻辑。
        /// </summary>
        protected override Task AddAsyncCore(FormNotify entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.AddAsyncCore(entity);
        }

        /// <summary>
        /// 更新实体核心逻辑。
        /// </summary>
        protected override Task<ReplaceOneResult> ReplaceAsyncCore(FormNotify entity)
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
            var items = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (items.Count != idList.Count)
            {
                throw new BadRequestException("提醒配置不存在");
            }

            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            foreach (var item in items)
            {
                evaluator.EnsureCanManageApp(item.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
