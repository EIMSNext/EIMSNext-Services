using System.Composition;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Integrations.Abstractions;
using EIMSNext.Core;
using EIMSNext.Feishu.Clients;

namespace EIMSNext.Auth.Integrations.Feishu
{
    [Export(typeof(IIntegrationProvider))]
    [ExportMetadata(MefMetadata.Id, IntegrationLoginType.Feishu)]
    public sealed class FeishuIntegrationProvider(FeishuClient client) : IntegrationProviderBase, IIntegrationProvider
    {
        public string Type => IntegrationLoginType.Feishu;

        public IntegrationProviderCapability Capability => new()
        {
            UnboundFailureMessage = "该飞书账号还未绑定到用户"
        };

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var token = await client.GetUserAccessTokenAsync(GetClientId(setting), GetClientSecret(setting), GetRequiredCode(payload.Code, "飞书"), cancellationToken);
            var userInfo = await client.GetUserInfoAsync(token.Data?.AccessToken ?? string.Empty, cancellationToken);
            var data = userInfo.Data ?? throw new InvalidOperationException("未能获取飞书用户信息");

            return new IntegrationAuthResult
            {
                IntegrationType = Type,
                OpenId = data.OpenId,
                UnionId = data.UnionId,
                TenantId = setting.TenantId,
                DisplayName = data.Name,
                Avatar = data.AvatarUrl
            };
        }

        public string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state)
        {
            return $"https://accounts.feishu.cn/open-apis/authen/v1/authorize?app_id={Encode(GetClientId(setting))}&redirect_uri={Encode(setting.RedirectUri)}&state={Encode(state)}";
        }
    }
}
