using System.Composition;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Core;
using EIMSNext.WxWork.Clients;

namespace EIMSNext.Auth.Integrations.WxWork
{
    [Export(typeof(IIntegrationProvider))]
    [ExportMetadata(MefMetadata.Id, IntegrationLoginType.WxWork)]
    public sealed class WxWorkIntegrationProvider(WxWorkClient client) : IntegrationProviderBase, IIntegrationProvider
    {
        public string Type => IntegrationLoginType.WxWork;

        public IntegrationProviderCapability Capability => new()
        {
            UnboundFailureMessage = "该企业微信账号还未绑定到用户"
        };

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var accessToken = await client.GetAccessTokenAsync(GetClientId(setting), GetClientSecret(setting), cancellationToken);
            var userInfo = await client.GetUserInfoAsync(accessToken.AccessToken, GetRequiredCode(payload.Code, "企业微信"), cancellationToken);
            var detail = string.IsNullOrWhiteSpace(userInfo.UserId)
                ? null
                : await client.GetUserDetailAsync(accessToken.AccessToken, userInfo.UserId, cancellationToken);

            return new IntegrationAuthResult
            {
                IntegrationType = Type,
                OpenId = userInfo.OpenId,
                ExternalUserId = string.IsNullOrWhiteSpace(userInfo.ExternalUserId) ? userInfo.UserId : userInfo.ExternalUserId,
                CorpId = setting.CorpId,
                DisplayName = detail?.Name ?? string.Empty,
                Avatar = detail?.Avatar ?? string.Empty
            };
        }

        public string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state)
        {
            return $"https://open.work.weixin.qq.com/wwopen/sso/qrConnect?appid={Encode(GetClientId(setting))}&agentid={Encode(setting.AgentId)}&redirect_uri={Encode(setting.RedirectUri)}&state={Encode(state)}";
        }
    }
}
