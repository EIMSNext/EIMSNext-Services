using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.DingTalk.Clients;

namespace EIMSNext.Auth.Services.Providers
{
    public sealed class DingTalkIntegrationProvider : IntegrationProviderBase, IIntegrationProvider
    {
        private readonly DingTalkClient _client;

        public DingTalkIntegrationProvider(DingTalkClient client)
        {
            _client = client;
        }

        public string Type => IntegrationLoginType.DingTalk;

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var accessToken = await _client.GetUserAccessTokenAsync(GetClientId(setting), GetClientSecret(setting), GetRequiredCode(payload.Code, "钉钉"), cancellationToken);
            var userInfo = await _client.GetUserInfoAsync(accessToken.AccessToken, cancellationToken);

            return new IntegrationAuthResult
            {
                IntegrationType = Type,
                OpenId = userInfo.OpenId,
                UnionId = userInfo.UnionId,
                DisplayName = userInfo.Nick,
                Avatar = userInfo.AvatarUrl
            };
        }

        public string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state)
        {
            return $"https://login.dingtalk.com/oauth2/auth?redirect_uri={Encode(setting.RedirectUri)}&response_type=code&client_id={Encode(GetClientId(setting))}&scope=openid&state={Encode(state)}&prompt=consent";
        }
    }
}
