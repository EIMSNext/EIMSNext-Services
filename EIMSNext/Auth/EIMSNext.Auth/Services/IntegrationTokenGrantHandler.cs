using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Auth.Services
{
    public sealed class IntegrationTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly IIntegrationAuthService _integrationAuthService;
        private readonly IAuditLoginService _auditLoginService;

        public IntegrationTokenGrantHandler(
            IIntegrationAuthService integrationAuthService,
            IAuditLoginService auditLoginService,
            IHttpContextAccessor contextAccessor)
            : base(contextAccessor)
        {
            _integrationAuthService = integrationAuthService;
            _auditLoginService = auditLoginService;
        }

        public string GrantType => CustomGrantType.Integration;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var integrationType = request.Username;
            try
            {
                var user = await _integrationAuthService.ValidateAsync(integrationType, request.Password, cancellationToken);
                if (user == null)
                {
                    var reason = GetFailureReason(integrationType);
                    await _auditLoginService.AddAuditLogin(CreateFailureAudit(integrationType, reason, GrantType));
                    return TokenRequestResult.Failure(Errors.InvalidGrant, reason);
                }

                var subject = user.Email;
                if (string.IsNullOrWhiteSpace(subject))
                {
                    subject = user.Phone;
                }

                if (string.IsNullOrWhiteSpace(subject))
                {
                    subject = user.Id;
                }

                var authenticationTime = DateTimeOffset.UtcNow;
                var claims = CreateUserClaims(subject, user, authenticationTime);
                await _auditLoginService.AddAuditLogin(CreateSuccessAudit(subject, user, claims, GrantType));
                return TokenRequestResult.Success(subject, GrantType, client.AccessTokenLifetime, scopes, claims);
            }
            catch (InvalidOperationException ex)
            {
                await _auditLoginService.AddAuditLogin(CreateFailureAudit(integrationType, ex.Message, GrantType));
                return TokenRequestResult.Failure(Errors.InvalidGrant, ex.Message);
            }
        }

        private static string GetFailureReason(string? integrationType)
        {
            return integrationType?.ToLowerInvariant() switch
            {
                IntegrationLoginType.WeChat => "该微信账号还未绑定到用户",
                IntegrationLoginType.WxWork => "该企业微信账号还未绑定到用户",
                IntegrationLoginType.DingTalk => "该钉钉账号还未绑定到用户",
                IntegrationLoginType.Feishu => "该飞书账号还未绑定到用户",
                _ => "第三方集成登录失败"
            };
        }
    }
}
