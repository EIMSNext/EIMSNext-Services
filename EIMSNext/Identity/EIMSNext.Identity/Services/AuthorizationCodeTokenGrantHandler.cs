using EIMSNext.Entities;
using EIMSNext.Identity.Abstractions;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace EIMSNext.Identity.Services
{
    /// <summary>
    /// authorization_code grant：按授权码前缀分发。
    /// - <c>lg_</c>：portal 签发的一次性本站授权码，从 IAuthorizationCodeStore 消费；
    /// - <c>wx_/ww_/dd_/fs_</c>：第三方集成登录回传的第三方 code，后端直接完成
    ///   「第三方 code → 第三方 token → 绑定用户 → 本站 token」，调用方无需经手第三方 token。
    /// </summary>
    public sealed class AuthorizationCodeTokenGrantHandler : TokenGrantHandlerBase, ITokenGrantHandler
    {
        private const string LocalCodePrefix = "lg_";

        /// <summary>第三方 code 前缀 → IntegrationLoginType</summary>
        private static readonly Dictionary<string, string> IntegrationPrefixes = new(StringComparer.Ordinal)
        {
            ["wx_"] = IntegrationLoginType.WeChat,
            ["ww_"] = IntegrationLoginType.WxWork,
            ["dd_"] = IntegrationLoginType.DingTalk,
            ["fs_"] = IntegrationLoginType.Feishu
        };

        private readonly IAuthorizationCodeStore _authorizationCodeStore;
        private readonly IIntegrationAuthService _integrationAuthService;
        private readonly IUserService _userService;

        public AuthorizationCodeTokenGrantHandler(
            IAuthorizationCodeStore authorizationCodeStore,
            IIntegrationAuthService integrationAuthService,
            IUserService userService,
            IIdentityLoginAuditService auditLoginService,
            IHttpContextAccessor contextAccessor)
            : base(auditLoginService, contextAccessor)
        {
            _authorizationCodeStore = authorizationCodeStore;
            _integrationAuthService = integrationAuthService;
            _userService = userService;
        }

        public string GrantType => CustomGrantType.AuthorizationCode;

        public async Task<TokenRequestResult> HandleAsync(Client client, OpenIddictRequest request, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
        {
            var code = request.Code ?? string.Empty;
            if (code.StartsWith(LocalCodePrefix, StringComparison.Ordinal))
            {
                return await HandleLocalCodeAsync(client, code[LocalCodePrefix.Length..], request.ClientId, scopes);
            }

            if (TryResolveIntegrationType(code, out var integrationType, out var thirdPartyCode))
            {
                return await HandleIntegrationCodeAsync(client, integrationType, thirdPartyCode, scopes);
            }

            return TokenRequestResult.Failure(Errors.InvalidGrant, "授权码无效或已过期");
        }

        private async Task<TokenRequestResult> HandleLocalCodeAsync(Client client, string code, string? clientId, IReadOnlyList<string> scopes)
        {
            var payload = await _authorizationCodeStore.TryConsumeAsync(code, clientId);
            if (payload == null)
            {
                await AddIdentityLoginAudit(CreateFailureAudit("authorization_code", "授权码无效或已过期", GrantType));
                return TokenRequestResult.Failure(Errors.InvalidGrant, "授权码无效或已过期");
            }

            var user = _userService.FindById(payload.UserId);
            if (user == null)
            {
                await AddIdentityLoginAudit(CreateFailureAudit(payload.Username, "授权码对应的用户不存在", GrantType));
                return TokenRequestResult.Failure(Errors.InvalidGrant, "授权码无效或已过期");
            }

            var authenticationTime = DateTimeOffset.UtcNow;
            var claims = CreateUserClaims(payload.Username, user, authenticationTime);
            // 会话续签（authorizeCodeSession 签发的 code）不是登录行为：
            // 同一次登录中 portal 与调用方各消费一个 code，若都记审计会出现重复登录日志。
            if (!payload.IsSessionRenewal)
            {
                await AddIdentityLoginAudit(CreateSuccessAudit(payload.Username, user, claims, GrantType));
            }
            return TokenRequestResult.Success(payload.Username, GrantType, client.AccessTokenLifetime, scopes, claims);
        }

        private async Task<TokenRequestResult> HandleIntegrationCodeAsync(Client client, string integrationType, string code, IReadOnlyList<string> scopes)
        {
            try
            {
                var result = await _integrationAuthService.ValidateAsync(integrationType, code);
                if (!result.Succeeded || result.User == null)
                {
                    var reason = string.IsNullOrWhiteSpace(result.FailureMessage) ? "第三方集成登录失败" : result.FailureMessage;
                    await AddIdentityLoginAudit(CreateFailureAudit(integrationType, reason, GrantType));
                    return TokenRequestResult.Failure(Errors.InvalidGrant, reason);
                }

                var user = result.User;
                var subject = ResolveSubject(user);
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

        internal static bool TryResolveIntegrationType(string code, out string integrationType, out string thirdPartyCode)
        {
            foreach (var (prefix, type) in IntegrationPrefixes)
            {
                if (code.StartsWith(prefix, StringComparison.Ordinal))
                {
                    integrationType = type;
                    thirdPartyCode = code[prefix.Length..];
                    return true;
                }
            }

            integrationType = string.Empty;
            thirdPartyCode = string.Empty;
            return false;
        }

        private static string ResolveSubject(User user)
        {
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }

            if (!string.IsNullOrWhiteSpace(user.Phone))
            {
                return user.Phone;
            }

            return user.Id;
        }
    }
}
