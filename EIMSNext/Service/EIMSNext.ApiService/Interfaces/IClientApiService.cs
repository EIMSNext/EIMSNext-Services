using EIMSNext.Auth.Entities;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.ApiService.ViewModels;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// OAuth 客户端的 API 服务接口。
    /// </summary>
    public interface IClientApiService : IApiService<Client, ClientViewModel>
    {
        /// <summary>创建客户端：自动生成 ClientId + ApiKey + 初始 Secret，缓存明文 5 分钟。</summary>
        Task<ClientCredentials> CreateAsync(ClientRequest input);

        /// <summary>read-modify-write 更新：忽略输入的 ClientSecrets/ClientId/ApiKey，保留 DB 原值。</summary>
        Task<Client> UpdateAsync(string id, ClientRequest input);

        /// <summary>生成新的 ClientSecret：返回明文，旧 Secret 失效。</summary>
        Task<ClientCredentials> GenerateSecretAsync(string id);

        /// <summary>生成新的 ApiKey：返回新 ApiKey。</summary>
        Task<ClientCredentials> GenerateApiKeyAsync(string id);

        /// <summary>查询当前可见的明文凭证。命中 5 分钟缓存才返回 clientSecret。</summary>
        Task<ClientCredentials> RevealAsync(string id);

        /// <summary>
        /// OData PATCH 直接调用：保留 ClientSecrets/ClientId/ApiKey/CorpId 不变，
        /// 仅更新输入中提供的可写字段（Enabled/ClientName/AllowedGrantTypes/AllowedScopes 等）。
        /// </summary>
        Task PatchAsync(string id, Client patch);

        /// <summary>按 CorpId 范围查询所有 Client（OData 不直接支持时使用）。</summary>
        Task<List<Client>> ListByCorpAsync();
    }

    /// <summary>
    /// 创建/生成后返回的明文凭证。
    /// 包含客户端 ID、明文密钥（仅首次可见）、API Key。
    /// </summary>
    public class ClientCredentials
    {
        /// <summary>对外公开的 ClientId。</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>明文 ClientSecret（仅在 create / generate-secret 端点的首次返回中可见）。</summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>明文 ApiKey（仅在 create / generate-api-key 端点返回）。</summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}
