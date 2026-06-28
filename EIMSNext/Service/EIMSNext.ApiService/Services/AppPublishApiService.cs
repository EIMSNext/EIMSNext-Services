using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Repositories;
using EIMSNext.Service.Contracts;
using EIMSNext.Service.Entities;
using HKH.Mef2.Integration;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 应用市场发布 API 服务。
    /// 负责将 <see cref="AppDef"/> 升级为 <see cref="AppProfile"/>（带模板），
    /// 供 <c>PlatAdmin</c> 通过 POST /appdef/{id}/publish 调用。
    /// </summary>
    public class AppPublishApiService : ApiServiceBase
    {
        public AppPublishApiService(IResolver resolver) : base(resolver)
        {
        }

        private IAppPublishService PublishService => Resolver.Resolve<IAppPublishService>();
        private IRepository<AppProfile> AppProfileRepository => Resolver.GetRepository<AppProfile>();

        /// <summary>
        /// 将已存在的 <see cref="AppDef"/> 升级为可被应用商店浏览/安装的 <see cref="AppProfile"/>。
        /// 同一 <c>AppDef</c> 重复发布会走 upsert 路径，<c>AppProfile</c> 不会重复。
        /// </summary>
        /// <param name="appDefId">要发布的 <see cref="AppDef"/> Id。</param>
        /// <returns>新创建或已更新的 <see cref="AppProfile"/> Id。</returns>
        public async Task<string> PublishAsync(string appDefId)
        {
            if (string.IsNullOrWhiteSpace(appDefId))
            {
                throw new BadRequestException("appDefId 不能为空");
            }

            var templateId = await PublishService.PublishAsync(appDefId);

            var profile = AppProfileRepository.Queryable
                .FirstOrDefault(x => x.TemplateId == templateId && !x.DeleteFlag);

            if (profile != null)
            {
                await StampAuthorAsync(profile);
            }

            return profile?.Id ?? templateId;
        }

        /// <summary>
        /// 在首次发布时把当前操作者写入 <see cref="AppProfile.Author"/>，便于审计。
        /// 仅在 <c>Author</c> 为空时填充，避免覆盖已有作者。
        /// </summary>
        private async Task StampAuthorAsync(AppProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Author))
            {
                return;
            }

            var author = IdentityContext?.CurrentUser?.Name;
            if (string.IsNullOrWhiteSpace(author))
            {
                return;
            }

            profile.Author = author!;
            await AppProfileRepository.ReplaceAsync(profile);
        }
    }
}
