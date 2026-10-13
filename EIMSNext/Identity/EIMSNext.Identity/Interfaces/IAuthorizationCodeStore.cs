namespace EIMSNext.Identity.Interfaces
{
    /// <summary>
    /// 门户签发的一次性授权码存储（lg_ 前缀）：短时效、绑定签发时的 client_id，
    /// 落分布式缓存以支持 Identity 多实例部署。
    /// </summary>
    public interface IAuthorizationCodeStore
    {
        /// <summary>签发授权码，返回随机 code。</summary>
        Task<string> IssueAsync(string userId, string username, string? clientId, TimeSpan ttl, CancellationToken cancellationToken = default);

        /// <summary>消费授权码：有效且 client 匹配时返回签发载荷（无论成败均立即作废，防重放）。</summary>
        Task<AuthorizationCodePayload?> TryConsumeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
    }

    public sealed record AuthorizationCodePayload(string UserId, string Username, string? ClientId);
}
