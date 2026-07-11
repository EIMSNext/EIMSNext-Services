using HKH.Mef2.Integration;
using EIMSNext.Common;
using EIMSNext.Service.Entities;
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
    }
}
