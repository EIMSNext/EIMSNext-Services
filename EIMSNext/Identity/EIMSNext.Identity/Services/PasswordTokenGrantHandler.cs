using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Identity.Services
{
    public sealed class PasswordTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly IUserService _userService;

        public PasswordTokenGrantHandler(
            IUserService userService,
            IIdentityLoginAuditService auditLoginService,
            IHttpContextAccessor contextAccessor)
            : base(auditLoginService, contextAccessor)
        {
            _userService = userService;
        }

        public string GrantType => GrantTypes.Password;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var username = request.Username;
            var password = request.Password;
            var user = string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
                ? null
                : _userService.Validate(username, password);

            if (user == null)
            {
                await AddIdentityLoginAudit(CreateFailureAudit(username, "用户不存在或密码错误"));
                return TokenRequestResult.Failure(Errors.InvalidGrant, "用户不存在或密码错误");
            }

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = CreateUserClaims(username!, user, authenticationTime);
            await AddIdentityLoginAudit(CreateSuccessAudit(username!, user, claims, "password"));
            return TokenRequestResult.Success(username, "password", client.AccessTokenLifetime, scopes, claims);
        }
    }
}
