using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.WeChat.Clients;

namespace EIMSNext.Auth.Services.Providers
{
    public sealed class WeChatIntegrationProvider : IntegrationProviderBase, IIntegrationProvider
    {
        private readonly WeChatOpenClient _client;

        public WeChatIntegrationProvider(WeChatOpenClient client)
        {
            _client = client;
        }

        public string Type => IntegrationLoginType.WeChat;

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var token = await _client.ExchangeCodeAsync(GetClientId(setting), GetClientSecret(setting), GetRequiredCode(payload.Code, "微信"), cancellationToken);
            var userInfo = await _client.GetUserInfoAsync(token.AccessToken, token.OpenId, cancellationToken);

            return new IntegrationAuthResult
            {
                IntegrationType = Type,
                OpenId = token.OpenId,
                UnionId = string.IsNullOrWhiteSpace(token.UnionId) ? userInfo.UnionId : token.UnionId,
                DisplayName = userInfo.NickName,
                Avatar = userInfo.HeadImageUrl
            };
        }

        public string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state)
        {
            return $"https://open.weixin.qq.com/connect/qrconnect?appid={Encode(GetClientId(setting))}&redirect_uri={Encode(setting.RedirectUri)}&response_type=code&scope=snsapi_login&state={Encode(state)}#wechat_redirect";
        }
    }
}
