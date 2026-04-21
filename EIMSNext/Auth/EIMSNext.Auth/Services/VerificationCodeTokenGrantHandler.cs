using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Services
{
    public sealed class VerificationCodeTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IAuditLoginService _auditLoginService;

        public VerificationCodeTokenGrantHandler(
            IVerificationCodeService verificationCodeService,
            IAuditLoginService auditLoginService,
            IHttpContextAccessor contextAccessor)
            : base(contextAccessor)
        {
            _verificationCodeService = verificationCodeService;
            _auditLoginService = auditLoginService;
        }

        public string GrantType => CustomGrantType.VerificationCode;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var username = request.Username;
            var verifyCode = request.Password;
            var user = _verificationCodeService.Validate(username, verifyCode);

            if (user == null)
            {
                await _auditLoginService.AddAuditLogin(CreateFailureAudit(username, "用户不存在或验证码错误"));
                return TokenRequestResult.Failure(OpenIddictConstants.Errors.InvalidGrant, "用户不存在或验证码错误");
            }

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = CreateUserClaims(username!, user, authenticationTime);
            await _auditLoginService.AddAuditLogin(CreateSuccessAudit(username!, user, claims, "verifycode"));
            return TokenRequestResult.Success(username, CustomGrantType.VerificationCode, client.AccessTokenLifetime, scopes, claims);
        }
    }
}
