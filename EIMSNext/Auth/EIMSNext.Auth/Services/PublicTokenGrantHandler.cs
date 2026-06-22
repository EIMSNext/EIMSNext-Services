using System.Security.Claims;
using EIMSNext.ApiService;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Services
{
    public sealed class PublicTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly IPublicTokenService _publicTokenService;

        public PublicTokenGrantHandler(IPublicTokenService publicTokenService, IHttpContextAccessor contextAccessor)
            : base(contextAccessor)
        {
            _publicTokenService = publicTokenService;
        }

        public string GrantType => CustomGrantType.Public;

        public Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var publicScope = ResolvePublicScope(request);
            if (publicScope == PublicScope.None)
            {
                return Task.FromResult(TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidRequest, "公开访问 scope 不能为空"));
            }

            var subject = _publicTokenService.Validate(request.Username, request.Password, publicScope);
            if (subject == null)
            {
                return Task.FromResult(TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidGrant, "公开访问凭证无效"));
            }

            var username = request.Username!;
            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = new List<Claim>
            {
                new(AuthClaimTypes.Subject, username),
                new(AuthClaimTypes.Name, "public"),
                new(AuthClaimTypes.Id, username),
                new(AuthClaimTypes.Corp, subject.CorpId),
                new(AuthClaimTypes.IdentityType, "Public"),
                new(AuthClaimTypes.PublicTargetId, subject.TargetId),
                new(AuthClaimTypes.PublicScope, ((int)publicScope).ToString(), ClaimValueTypes.Integer32),
                new(AuthClaimTypes.AuthTime, authenticationTime.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            return Task.FromResult(TokenRequestResult.Success(username, CustomGrantType.Public, client.AccessTokenLifetime, scopes, claims));
        }

        private static PublicScope ResolvePublicScope(OpenIddictRequest request)
        {
            var raw = request.GetParameter("scope")?.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return PublicScope.None;

            var flags = PublicScope.None;
            foreach (var token in raw.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse<PublicScope>(token, ignoreCase: true, out var parsed))
                {
                    flags |= parsed;
                }
            }
            return flags;
        }
    }
}
