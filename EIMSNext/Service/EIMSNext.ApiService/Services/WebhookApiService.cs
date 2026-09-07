using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 数据推送（Webhook）的 API 服务。
    /// </summary>
    public class WebhookApiService(IResolver resolver) : ApiServiceBase<Webhook, WebhookViewModel, IWebhookService>(resolver)
    {
        /// <summary>
        /// 获取按权限过滤后的数据推送视图查询。
        /// </summary>
        /// <returns>视图模型的可查询对象。</returns>
        protected override IQueryable<WebhookViewModel> FilterByPermission()
        {
            var query = base.FilterByPermission();
            var evaluator = Resolver.Resolve<TenantAccessEvaluator>();
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

        /// <summary>
        /// 新增数据推送。
        /// </summary>
        /// <param name="entity">数据推送实体。</param>
        protected override Task AddAsyncCore(Webhook entity)
        {
            EnsureCanManageWebhook(entity);
            return base.AddAsyncCore(entity);
        }

        /// <summary>
        /// 替换数据推送。
        /// </summary>
        /// <param name="entity">数据推送实体。</param>
        /// <returns>替换结果。</returns>
        protected override Task<ReplaceOneResult> ReplaceAsyncCore(Webhook entity)
        {
            EnsureCanManageWebhook(entity);
            return base.ReplaceAsyncCore(entity);
        }

        /// <summary>
        /// 删除数据推送。
        /// </summary>
        /// <param name="ids">数据推送 ID 集合。</param>
        /// <returns>删除结果。</returns>
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
                Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(item.AppId);
            }

            return await base.DeleteAsyncCore(idList);
        }

        private void EnsureCanManageWebhook(Webhook entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);

            if (!IsAllowedWebhookUrl(entity.Url))
            {
                throw new BadRequestException("推送地址必须是有效的 HTTP 或 HTTPS 地址");
            }

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

        private static bool IsAllowedWebhookUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && !string.IsNullOrWhiteSpace(uri.Host)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
