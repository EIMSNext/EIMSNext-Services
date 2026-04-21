using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Services
{
    public sealed class SingleSignOnTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly ISingleSignOnService _singleSignOnService;
        private readonly IAuditLoginService _auditLoginService;

        public SingleSignOnTokenGrantHandler(
            ISingleSignOnService singleSignOnService,
            IAuditLoginService auditLoginService,
            IHttpContextAccessor contextAccessor)
            : base(contextAccessor)
        {
            _singleSignOnService = singleSignOnService;
            _auditLoginService = auditLoginService;
        }

        public string GrantType => CustomGrantType.SingleSignOn;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var username = request.Username;
            var secret = request.Password;
            var user = _singleSignOnService.Validate(username, secret);

            if (user == null)
            {
                await _auditLoginService.AddAuditLogin(CreateFailureAudit(username, "用户不存在或密码错误"));
                return TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidGrant, "用户不存在或密码错误");
            }

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = CreateUserClaims(username!, user, authenticationTime);
            await _auditLoginService.AddAuditLogin(CreateSuccessAudit(username!, user, claims, "sso"));
            return TokenRequestResult.Success(username, CustomGrantType.SingleSignOn, client.AccessTokenLifetime, scopes, claims);
        }
    }
}
