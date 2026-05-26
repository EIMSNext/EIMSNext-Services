using System.Composition;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Core;
using EIMSNext.DingTalk.Clients;

namespace EIMSNext.Auth.Integrations.DingTalk
{
    [Export(typeof(IIntegrationProvider))]
    [ExportMetadata(MefMetadata.Id, IntegrationLoginType.DingTalk)]
    public sealed class DingTalkIntegrationProvider(DingTalkClient client) : IntegrationProviderBase, IIntegrationProvider
    {
        public string Type => IntegrationLoginType.DingTalk;

        public IntegrationProviderCapability Capability => new()
        {
            UnboundFailureMessage = "该钉钉账号还未绑定到用户"
        };

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var token = await client.GetUserAccessTokenAsync(GetClientId(setting), GetClientSecret(setting), GetRequiredCode(payload.Code, "钉钉"), cancellationToken);
            var userInfo = await client.GetUserInfoAsync(token.AccessToken, cancellationToken);

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
