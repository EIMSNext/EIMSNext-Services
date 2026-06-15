using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class FormListViewApiService(IResolver resolver) : ApiServiceBase<FormListView, FormListViewViewModel, IFormListViewService>(resolver)
    {
        protected override Task AddAsyncCore(FormListView entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(FormListView entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            return base.ReplaceAsyncCore(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var views = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (views.Count != idList.Count)
            {
                throw new BadRequestException("视图不存在");
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var view in views)
            {
                evaluator.EnsureCanManageApp(view.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
    }
}
