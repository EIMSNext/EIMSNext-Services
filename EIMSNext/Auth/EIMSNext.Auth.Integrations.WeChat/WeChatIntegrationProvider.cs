using System.Composition;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Core;
using EIMSNext.WeChat.Clients;

namespace EIMSNext.Auth.Integrations.WeChat
{
    [Export(typeof(IIntegrationProvider))]
    [ExportMetadata(MefMetadata.Id, IntegrationLoginType.WeChat)]
    public sealed class WeChatIntegrationProvider(WeChatOpenClient client) : IntegrationProviderBase, IIntegrationProvider
    {
        public string Type => IntegrationLoginType.WeChat;

        public IntegrationProviderCapability Capability => new()
        {
            UnboundFailureMessage = "该微信账号还未绑定到用户",
            CanAutoProvisionUser = true,
            DefaultUserName = "微信用户"
        };

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var token = await client.ExchangeCodeAsync(GetClientId(setting), GetClientSecret(setting), GetRequiredCode(payload.Code, "微信"), cancellationToken);
            var userInfo = await client.GetUserInfoAsync(token.AccessToken, token.OpenId, cancellationToken);

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
