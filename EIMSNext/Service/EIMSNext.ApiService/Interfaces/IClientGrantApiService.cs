using EIMSNext.ApiService.ViewModels;
using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// 客户端授权（ClientGrant）的 API 服务接口。
    /// </summary>
    public interface IClientGrantApiService : IApiService<ClientGrant, ClientGrantViewModel>
    {
        /// <summary>按 ClientId 查 corp 范围内生效的授权记录。</summary>
        Task<ClientGrant?> GetActiveByClientIdAsync(string clientId);
    }
}
