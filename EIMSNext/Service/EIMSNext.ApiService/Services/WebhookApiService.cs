using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Service.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
	public class WebhookApiService(IResolver resolver) : ApiServiceBase<Webhook, WebhookViewModel, IWebhookService>(resolver)
	{
        protected override IQueryable<WebhookViewModel> FilterByPermission()
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

        protected override Task AddAsyncCore(Webhook entity)
        {
            EnsureCanManageWebhook(entity);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(Webhook entity)
        {
            EnsureCanManageWebhook(entity);
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
                throw new BadRequestException("数据推送不存在");
            }

            foreach (var item in items)
            {
                Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(item.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private void EnsureCanManageWebhook(Webhook entity)
        {
            Resolver.Resolve<AdminPermissionEvaluator>().EnsureCanManageApp(entity.AppId);

            if (string.IsNullOrWhiteSpace(entity.FormId))
            {
                throw new BadRequestException("推送表单不能为空");
            }

            var form = Resolver.GetRepository<FormDef>().Get(entity.FormId);
            if (form == null || form.CorpId != IdentityContext.CurrentCorpId || form.DeleteFlag || form.AppId != entity.AppId)
            {
                throw new BadRequestException("推送表单不存在");
            }
        }
    }
}
