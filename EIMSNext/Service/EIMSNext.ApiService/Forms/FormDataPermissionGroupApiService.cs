using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class FormDataPermissionGroupApiService(IResolver resolver) : ApiServiceBase<FormDataPermissionGroup, FormDataPermissionGroupViewModel, IFormDataPermissionGroupService>(resolver)
	{
        protected override Task AddAsyncCore(FormDataPermissionGroup entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageFormDataPermissionGroup(entity);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(FormDataPermissionGroup entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageFormDataPermissionGroup(entity);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var permissionGroups = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (permissionGroups.Count != idList.Count)
            {
                throw new BadRequestException("授权组不存在");
            }

            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
            foreach (var permissionGroup in permissionGroups)
            {
                evaluator.EnsureCanManageApp(permissionGroup.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
