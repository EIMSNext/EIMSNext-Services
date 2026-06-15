using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class FormNotifyApiService(IResolver resolver) : ApiServiceBase<FormNotify, FormNotifyViewModel, IFormNotifyService>(resolver)
	{
        protected override Task AddAsyncCore(FormNotify entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(FormNotify entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.ReplaceAsyncCore(entity);
        }

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

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var item in items)
            {
                evaluator.EnsureCanManageApp(item.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
