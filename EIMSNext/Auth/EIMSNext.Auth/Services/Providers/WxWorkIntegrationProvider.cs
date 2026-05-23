using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.WxWork.Clients;

namespace EIMSNext.Auth.Services.Providers
{
    public sealed class WxWorkIntegrationProvider : IntegrationProviderBase, IIntegrationProvider
    {
        private readonly WxWorkClient _client;

        public WxWorkIntegrationProvider(WxWorkClient client)
        {
            _client = client;
        }

        public string Type => IntegrationLoginType.WxWork;

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var accessToken = await _client.GetAccessTokenAsync(GetClientId(setting), GetClientSecret(setting), cancellationToken);
            var userInfo = await _client.GetUserInfoAsync(accessToken.AccessToken, GetRequiredCode(payload.Code, "企业微信"), cancellationToken);
            var detail = string.IsNullOrWhiteSpace(userInfo.UserId)
                ? null
                : await _client.GetUserDetailAsync(accessToken.AccessToken, userInfo.UserId, cancellationToken);

            return new IntegrationAuthResult
            {
                IntegrationType = Type,
                OpenId = userInfo.OpenId,
                ExternalUserId = string.IsNullOrWhiteSpace(userInfo.ExternalUserId) ? userInfo.UserId : userInfo.ExternalUserId,
                DisplayName = detail?.Name ?? string.Empty,
                Avatar = detail?.Avatar ?? string.Empty,
                CorpId = setting.CorpId
            };
        }

        public string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state)
        {
            return $"https://open.work.weixin.qq.com/wwopen/sso/qrConnect?appid={Encode(GetClientId(setting))}&agentid={Encode(setting.AgentId)}&redirect_uri={Encode(setting.RedirectUri)}&state={Encode(state)}";
        }
    }
}
