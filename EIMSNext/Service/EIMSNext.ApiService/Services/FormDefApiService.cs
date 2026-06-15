using HKH.Mef2.Integration;

using EIMSNext.ApiService.ViewModels;
using EIMSNext.Component;
using EIMSNext.Service.Entities;

using MongoDB.Driver;
using EIMSNext.Service.Contracts;
using EIMSNext.Common;

namespace EIMSNext.ApiService
{
    public class FormDefApiService(IResolver resolver) : ApiServiceBase<FormDef, FormDefViewModel, IFormDefService>(resolver)
	{
        public override Task AddAsync(FormDef entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            return base.AddAsync(entity);
        }

        public override Task<ReplaceOneResult> ReplaceAsync(FormDef entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);
            entity.Content.Items = Resolver.Resolve<FormLayoutParser>().Parse(entity.Content.Layout);
            ServiceContext.ScopeCache.Set(entity.Id, entity, Cache.DataVersion.New);

            return base.ReplaceAsync(entity);
        }

        protected override async Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            var idList = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var forms = CoreService.All()
                .Where(x => x.CorpId == IdentityContext.CurrentCorpId && !x.DeleteFlag && idList.Contains(x.Id))
                .ToList();

            if (forms.Count != idList.Count)
            {
                throw new BadRequestException("表单不存在");
            }

            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            foreach (var form in forms)
            {
                evaluator.EnsureCanManageApp(form.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }
    }
}
