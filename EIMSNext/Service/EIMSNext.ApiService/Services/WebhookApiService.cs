using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Entities;
using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Contracts;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Configuration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 数据推送（Webhook）的 API 服务。
    /// </summary>
    public class WebhookApiService(IResolver resolver, IConfiguration configuration) : ApiServiceBase<Webhook, IWebhookService>(resolver)
    {
        /// <summary>
        /// 获取按权限过滤后的数据推送视图查询。
        /// </summary>
        /// <returns>视图模型的可查询对象。</returns>
        protected override IQueryable<Webhook> FilterByPermission()
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
        protected override async Task AddAsyncCore(Webhook entity)
        {
            await EnsureCanManageWebhookAsync(entity);
            await base.AddAsyncCore(entity);
        }

        /// <summary>
        /// 替换数据推送。
        /// </summary>
        /// <param name="entity">数据推送实体。</param>
        /// <returns>替换结果。</returns>
        protected override async Task<int> ReplaceAsyncCore(Webhook entity)
        {
            await EnsureCanManageWebhookAsync(entity);
            return await base.ReplaceAsyncCore(entity);
        }

        /// <summary>
        /// 删除数据推送。
        /// </summary>
        /// <param name="ids">数据推送 ID 集合。</param>
        /// <returns>删除结果。</returns>
        protected override async Task<int> DeleteAsyncCore(IEnumerable<string> ids)
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

        private async Task EnsureCanManageWebhookAsync(Webhook entity)
        {
            Resolver.Resolve<TenantAccessEvaluator>().EnsureCanManageApp(entity.AppId);

            if (await WebhookUrlSafety.ResolveAsync(entity.Url, configuration).ConfigureAwait(false) is null)
            {
                throw new BadRequestException("推送地址必须是可访问的公网 HTTP 或 HTTPS 地址");
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

    }
}
