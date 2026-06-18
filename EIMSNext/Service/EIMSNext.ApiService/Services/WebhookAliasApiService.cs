using EIMSNext.ApiService.ViewModels;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public class WebhookAliasApiService(IResolver resolver) : ApiServiceBase<WebhookAlias, WebhookAliasViewModel, IWebhookAliasService>(resolver)
    {
        protected override IQueryable<WebhookAliasViewModel> FilterByPermission()
        {
            var query = base.FilterByPermission();
            var evaluator = Resolver.Resolve<AdminPermissionEvaluator>();
            if (evaluator.HasUnrestrictedManagementIdentity)
            {
                return query;
            }

            if (IdentityContext.IdentityType == IdentityType.AppAdmin)
            {
                var appIds = evaluator.GetSnapshot().ManageableAppIds;
                return query.Where(x => appIds.Contains(x.AppId));
            }

            return query.Where(x => false);
        }

        protected override Task AddAsyncCore(WebhookAlias entity)
        {
            EnsureCanManageAlias(entity);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(WebhookAlias entity)
        {
            EnsureCanManageAlias(entity);
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
                throw new BadRequestException("Webhook字段别名配置不存在");
            }

            foreach (var item in items)
            {
                Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(item.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private void EnsureCanManageAlias(WebhookAlias entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);

            if (string.IsNullOrWhiteSpace(entity.FormId))
            {
                throw new BadRequestException("表单ID不能为空");
            }

            var form = Resolver.GetRepository<FormDef>().Get(entity.FormId);
            if (form == null || form.CorpId != IdentityContext.CurrentCorpId || form.DeleteFlag || form.AppId != entity.AppId)
            {
                throw new BadRequestException("表单不存在");
            }
        }
    }
}
