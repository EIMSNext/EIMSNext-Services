using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Services
{
    public sealed class VerificationCodeTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly IVerificationCodeService _verificationCodeService;

        public VerificationCodeTokenGrantHandler(
            IVerificationCodeService verificationCodeService,
            IIdentityLoginAuditService auditLoginService,
            IHttpContextAccessor contextAccessor)
            : base(auditLoginService, contextAccessor)
        {
            _verificationCodeService = verificationCodeService;
        }

        public string GrantType => CustomGrantType.VerificationCode;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var username = request.Username;
            var verifyCode = request.Password;
            var user = _verificationCodeService.Validate(username, verifyCode);

            if (user == null)
            {
                await AddIdentityLoginAudit(CreateFailureAudit(username, "用户不存在或验证码错误"));
                return TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidGrant, "用户不存在或验证码错误");
            }

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = CreateUserClaims(username!, user, authenticationTime);
            await AddIdentityLoginAudit(CreateSuccessAudit(username!, user, claims, "verifycode"));
            return TokenRequestResult.Success(username, CustomGrantType.VerificationCode, client.AccessTokenLifetime, scopes, claims);
        }
    }
}
