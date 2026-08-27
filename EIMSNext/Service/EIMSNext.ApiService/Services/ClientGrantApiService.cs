using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Entities;
using EIMSNext.Service.Contracts;
using EIMSNext.ApiService.ViewModels;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 客户端授权 API 服务。
    /// 提供标准的 CRUD + <c>GetActiveByClientIdAsync</c>（给 <c>ClientPermissionCache</c> 用）。
    /// </summary>
    public class ClientGrantApiService(IResolver resolver)
        : ApiServiceBase<ClientGrant, ClientGrantViewModel, IClientGrantService>(resolver), IClientGrantApiService
    {
        protected override Task AddAsyncCore(ClientGrant entity)
        {
            ValidateGrant(entity);
            return base.AddAsyncCore(entity);
        }

        protected override Task<ReplaceOneResult> ReplaceAsyncCore(ClientGrant entity)
        {
            ValidateGrant(entity);
            return base.ReplaceAsyncCore(entity);
        }

        /// <summary>按 ClientId 查 corp 范围内生效的授权记录。</summary>
        public async Task<ClientGrant?> GetActiveByClientIdAsync(string clientId)
        {
            return await CoreService.Find(x =>
                    x.CorpId == IdentityContext.CurrentCorpId
                    && !x.DeleteFlag
                    && x.Enabled
                    && x.ClientId == clientId)
                .FirstOrDefaultAsync();
        }

        private static void ValidateGrant(ClientGrant entity)
        {
            if (!entity.AppScope.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                !entity.AppScope.Equals("partial", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("应用授权范围无效");
            }

            if (entity.AppScope.Equals("partial", StringComparison.OrdinalIgnoreCase) && entity.AppIds.Count == 0)
            {
                throw new BadRequestException("部分应用授权至少选择一个应用");
            }

            if (!entity.ApiScope.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                !entity.ApiScope.Equals("partial", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("接口授权范围无效");
            }

            if (entity.ApiScope.Equals("partial", StringComparison.OrdinalIgnoreCase) && entity.ResourceActions.Count == 0)
            {
                throw new BadRequestException("部分接口授权至少选择一个资源");
            }
        }
    }
}
