using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Services
{
    public sealed class SingleSignOnTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly ISingleSignOnService _singleSignOnService;

        public SingleSignOnTokenGrantHandler(
            ISingleSignOnService singleSignOnService,
            IIdentityLoginAuditService auditLoginService,
            IHttpContextAccessor contextAccessor)
            : base(auditLoginService, contextAccessor)
        {
            _singleSignOnService = singleSignOnService;
        }

        public string GrantType => CustomGrantType.SingleSignOn;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var username = request.Username;
            var secret = request.Password;
            var user = _singleSignOnService.Validate(username, secret);

            if (user == null)
            {
                await AddIdentityLoginAudit(CreateFailureAudit(username, "用户不存在或密码错误"));
                return TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidGrant, "用户不存在或密码错误");
            }

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = CreateUserClaims(username!, user, authenticationTime);
            await AddIdentityLoginAudit(CreateSuccessAudit(username!, user, claims, "sso"));
            return TokenRequestResult.Success(username, CustomGrantType.SingleSignOn, client.AccessTokenLifetime, scopes, claims);
        }
    }
}
