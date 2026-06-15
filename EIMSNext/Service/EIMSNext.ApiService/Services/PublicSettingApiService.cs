using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class PublicSettingApiService(IResolver resolver) : ApiServiceBase<PublicSetting, PublicSettingViewModel, IPublicSettingService>(resolver)
	{
        protected override Task AddAsyncCore(PublicSetting entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(PublicSetting entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var settings = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (settings.Count != idList.Count)
            {
                throw new BadRequestException("公开设置不存在");
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var setting in settings)
            {
                evaluator.EnsureCanManageApp(setting.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
