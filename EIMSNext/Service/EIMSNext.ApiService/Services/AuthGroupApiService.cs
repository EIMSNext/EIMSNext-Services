using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class AuthGroupApiService(IResolver resolver) : ApiServiceBase<AuthGroup, AuthGroupViewModel, IAuthGroupService>(resolver)
	{
        protected override Task AddAsyncCore(AuthGroup entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageAuthGroup(entity);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(AuthGroup entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageAuthGroup(entity);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var authGroups = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (authGroups.Count != idList.Count)
            {
                throw new BadRequestException("授权组不存在");
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var authGroup in authGroups)
            {
                evaluator.EnsureCanManageApp(authGroup.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
	}
}
