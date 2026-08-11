using System.Security.Claims;
using EIMSNext.ApiCore;
using EIMSNext.ApiCore.RateLimiting;
using EIMSNext.ApiService;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Services
{
    public sealed class PublicTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private const string UsernamePrefix = "public_";

        private readonly IPublicTokenService _publicTokenService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PublicTokenGrantHandler(IPublicTokenService publicTokenService, IAuditLoginService auditLoginService, IHttpContextAccessor contextAccessor)
            : base(auditLoginService, contextAccessor)
        {
            _publicTokenService = publicTokenService;
            _httpContextAccessor = contextAccessor;
        }

        public string GrantType => CustomGrantType.Public;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var publicScope = ResolvePublicScope(request);
            if (publicScope == PublicScope.None)
            {
                return TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidRequest, "公开访问 scope 不能为空");
            }

            var username = request.Username;
            var targetId = string.IsNullOrWhiteSpace(username) || !username.StartsWith(UsernamePrefix, StringComparison.Ordinal)
                ? "unknown"
                : username[UsernamePrefix.Length..];

            var ip = IpHelper.GetClientIp(_httpContextAccessor);
            var rateLimiter = _httpContextAccessor.HttpContext?.RequestServices?.GetService<PublicRateLimiter>();
            if (rateLimiter != null)
            {
                var rate = await rateLimiter.CheckAsync("token", targetId, ip);
                if (!rate.Allowed)
                {
                    return TokenRequestResult.Failure("rate_limited", "公开 token 申请过于频繁");
                }
            }

            var validation = _publicTokenService.Validate(request.Username, request.Password, publicScope);
            if (!validation.Succeeded)
            {
                return TokenRequestResult.Failure(validation.Error!, validation.ErrorDescription!);
            }

            var subject = validation.Subject!;

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = new List<Claim>
            {
                new(AuthClaimTypes.Subject, username!),
                new(AuthClaimTypes.Name, "public"),
                new(AuthClaimTypes.Id, username!),
                new(AuthClaimTypes.Corp, subject.CorpId),
                new(AuthClaimTypes.IdentityType, "Public"),
                new(AuthClaimTypes.PublicTargetId, subject.TargetId),
                new(AuthClaimTypes.PublicScope, publicScope.ToString().ToLowerInvariant()),
                new(AuthClaimTypes.AuthTime, authenticationTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            return TokenRequestResult.Success(username!, CustomGrantType.Public, client.AccessTokenLifetime, scopes, claims);
        }

        private static PublicScope ResolvePublicScope(OpenIddictRequest request)
        {
            var raw = request.GetParameter("scope")?.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return PublicScope.None;

            return Enum.TryParse<PublicScope>(raw, ignoreCase: true, out var parsed)
                && parsed is not PublicScope.None
                && IsSingleScope(parsed)
                ? parsed
                : PublicScope.None;
        }

        private static bool IsSingleScope(PublicScope scope)
        {
            var value = (int)scope;
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
