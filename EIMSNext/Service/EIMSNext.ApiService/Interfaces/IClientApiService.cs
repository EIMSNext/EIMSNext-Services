using EIMSNext.Auth.Entities;
using EIMSNext.ApiService.ViewModels;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// OAuth 客户端的 API 服务接口。
    /// </summary>
    public interface IClientApiService : IApiService<Client, ClientViewModel>
    {
        /// <summary>生成新的 ClientSecret：返回明文，旧 Secret 失效。</summary>
        Task<ClientCredentials> GenerateSecretAsync(string id);

        /// <summary>查询当前可见的明文凭证。命中 5 分钟缓存才返回 clientSecret。</summary>
        Task<ClientCredentials> RevealAsync(string id);
    }

    /// <summary>
    /// 查询/生成后返回的明文凭证。
    /// 包含客户端 ID、明文密钥（仅在缓存期或重新生成后可见）。
    /// </summary>
    public class ClientCredentials
    {
        /// <summary>对外公开的 Client Id。</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>明文 ClientSecret（仅在 create / generate-secret 端点的首次返回中可见）。</summary>
        public string ClientSecret { get; set; } = string.Empty;
    }
}
