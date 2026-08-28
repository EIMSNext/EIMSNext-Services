using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Identity.Services
{
    public sealed class IntegrationTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private readonly IIntegrationAuthService _integrationAuthService;

        public IntegrationTokenGrantHandler(
            IIntegrationAuthService integrationAuthService,
            IIdentityLoginAuditService identityLoginAuditService,
            IHttpContextAccessor contextAccessor)
            : base(identityLoginAuditService, contextAccessor)
        {
            _integrationAuthService = integrationAuthService;
        }

        public string GrantType => CustomGrantType.Integration;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var integrationType = request.Username;
            try
            {
                var result = await _integrationAuthService.ValidateAsync(integrationType, request.Password, cancellationToken);
                if (!result.Succeeded)
                {
                    var reason = string.IsNullOrWhiteSpace(result.FailureMessage) ? "第三方集成登录失败" : result.FailureMessage;
                    await AddIdentityLoginAudit(CreateFailureAudit(integrationType, reason, GrantType));
                    return TokenRequestResult.Failure(Errors.InvalidGrant, reason);
                }

                var user = result.User!;

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
                await AddIdentityLoginAudit(CreateSuccessAudit(subject, user, claims, GrantType));
                return TokenRequestResult.Success(subject, GrantType, client.AccessTokenLifetime, scopes, claims);
            }
            catch (InvalidOperationException ex)
            {
                await AddIdentityLoginAudit(CreateFailureAudit(integrationType, ex.Message, GrantType));
                return TokenRequestResult.Failure(Errors.InvalidGrant, ex.Message);
            }
        }
    }
}
