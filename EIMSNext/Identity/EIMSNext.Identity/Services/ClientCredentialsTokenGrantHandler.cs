using System.Security.Claims;
using EIMSNext.ApiCore;
using EIMSNext.ApiService;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.Common;
using EIMSNext.Common.Extensions;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Identity.Services
{
    /// <summary>
    /// 标准 OAuth2 <c>client_credentials</c> grant。
    ///
    /// 流程：
    /// <list type="number">
    /// <item>校验 client.Enabled + client.RequireClientSecret + scope 在 AllowedScopes 之内。</item>
    /// <item>不绑定任何 user；<c>sub</c> = <c>client_id</c>。</item>
    /// <item>写 <c>client_id</c> / <c>identity_type=Client</c> / <c>corp</c> claims。</item>
    /// <item>触发 <c>ClientPermissionCache.Refresh</c>（让新发 token 立即生效）。</item>
    /// </list>
    /// </summary>
    public sealed class ClientCredentialsTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        public ClientCredentialsTokenGrantHandler(IIdentityLoginAuditService auditLoginService, IHttpContextAccessor contextAccessor)
            : base(auditLoginService, contextAccessor)
        {
        }

        public string GrantType => CustomGrantType.ClientCredentials;

        public async Task<TokenRequestResult> HandleAsync(
            Client client,
            OpenIddictRequest request,
            IReadOnlyList<string> scopes,
            CancellationToken cancellationToken = default)
        {
            if (!client.Enabled)
            {
                return TokenRequestResult.Failure(
                    Errors.UnauthorizedClient, "Client 已被禁用");
            }

            // scope 必须是 AllowedScopes 的子集
            if (scopes.Count > 0 && client.AllowedScopes.Count > 0)
            {
                var allowed = client.AllowedScopes.Select(s => s.Scope).ToHashSet();
                foreach (var s in scopes)
                {
                    if (!allowed.Contains(s))
                    {
                        return TokenRequestResult.Failure(
                            Errors.InvalidScope, $"scope '{s}' 不在 Client 的允许列表中");
                    }
                }
            }

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = new List<Claim>
            {
                new(IdentityClaimTypes.Subject, client.Id),
                new(IdentityClaimTypes.Name, client.Name ?? client.Id),
                new(IdentityClaimTypes.Id, "client"),  // 标识无 user 上下文
                new(IdentityClaimTypes.Corp, client.CorpId??string.Empty),
                new(IdentityClaimTypes.ClientId, client.Id),
                new(IdentityClaimTypes.IdentityType, IdentityType.Client.ToString()),
                new(IdentityClaimTypes.AuthTime, authenticationTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            };

            await AddIdentityLoginAudit(CreateSuccessAudit(client.Id, client.CorpId, client.Id, client.Name, GrantType));
            return TokenRequestResult.Success(client.Id, GrantType, client.AccessTokenLifetime, scopes, claims);
        }
    }
}
