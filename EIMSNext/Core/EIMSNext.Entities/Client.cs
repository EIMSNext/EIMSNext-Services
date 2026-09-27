using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace EIMSNext.Entities
{
    /// <summary>
    /// OAuth 客户端。
    ///
    /// 用于 client_credentials 等开放平台授权流程。
    /// <see cref="ClientSecrets"/> 仅在创建/重新生成时由服务端设置；
    /// 存储的永远是 SHA-256 哈希（见 <see cref="StringExtensions.Sha256"/>）。
    ///
    /// 客户端的"开放平台资源授权"信息（应用范围、API 范围、IP 白名单）
    /// 存储在独立的 <c>EIMSNext.Entities.ClientGrant</c> 实体中，通过
    /// <c>Id</c> 关联。
    /// </summary>
    public class Client : CorpEntityBase
    {
        /// <summary>Client 自身是否启用。false 时 <c>client_credentials</c> grant 直接拒绝。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 客户端密钥列表（哈希后存储）。OData 不会序列化此字段；
        /// 明文只能在创建后的短期缓存或 generate-secret 端点取得。
        /// </summary>
        public List<ClientSecret> ClientSecrets { get; set; } = [];

        /// <summary>调用 token 端点时是否校验 ClientSecret。false 表示匿名 client（公开流程）。</summary>
        public bool RequireClientSecret { get; set; } = true;

        /// <summary>客户端可读的显示名（仅用于 UI，不参与鉴权）。</summary>
        public string? Name { get; set; }

        /// <summary>允许的 grant_type 列表，例如 <c>client_credentials</c>、<c>password</c>。</summary>
        public List<ClientGrantType> AllowedGrantTypes { get; set; } = new();

        /// <summary>允许的 scope 列表，由 token 端点校验。</summary>
        public List<ClientScope> AllowedScopes { get; set; } = new();

        /// <summary>id_token 有效期（秒），默认 8 小时。</summary>
        public int IdentityTokenLifetime { get; set; } = 28800;

        /// <summary>access_token 有效期（秒），默认 8 小时。</summary>
        public int AccessTokenLifetime { get; set; } = 28800;

        /// <summary>内部 API Key（nanoid 36），由服务端生成并维护。OData 上为只读。</summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}
