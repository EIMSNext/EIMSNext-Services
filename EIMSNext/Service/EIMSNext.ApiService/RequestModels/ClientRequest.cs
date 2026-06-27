using EIMSNext.Auth.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// OAuth Client 的 OData POST/PUT/PATCH 输入模型。
    ///
    /// 故意省略 <c>ClientSecrets</c>、<c>ClientId</c>、<c>ApiKey</c>：
    /// <list type="bullet">
    /// <item><c>ClientSecrets</c> 由 Create/Regenerate 端点控制（<see cref="ClientRequestModelConfiguration"/> 同样 Ignore）。</item>
    /// <item><c>ClientId</c>、<c>ApiKey</c> 由系统生成，OData 上为只读。</item>
    /// </list>
    /// </summary>
    public class ClientRequest : RequestBase
    {
        public bool Enabled { get; set; } = true;
        public bool RequireClientSecret { get; set; } = true;
        public string? ClientName { get; set; }
        public List<ClientGrantType> AllowedGrantTypes { get; set; } = new();
        public List<ClientScope> AllowedScopes { get; set; } = new();
        public int IdentityTokenLifetime { get; set; } = 28800;
        public int AccessTokenLifetime { get; set; } = 28800;
    }
}
