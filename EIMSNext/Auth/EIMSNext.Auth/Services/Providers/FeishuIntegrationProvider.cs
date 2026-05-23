using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.Feishu.Clients;

namespace EIMSNext.Auth.Services.Providers
{
    public sealed class FeishuIntegrationProvider : IntegrationProviderBase, IIntegrationProvider
    {
        private readonly FeishuClient _client;

        public FeishuIntegrationProvider(FeishuClient client)
        {
            _client = client;
        }

        public string Type => IntegrationLoginType.Feishu;

        public async Task<IntegrationAuthResult> AuthenticateAsync(IntegrationLoginSetting setting, IntegrationAuthPayload payload, CancellationToken cancellationToken = default)
        {
            var accessToken = await _client.GetUserAccessTokenAsync(GetClientId(setting), GetClientSecret(setting), GetRequiredCode(payload.Code, "飞书"), cancellationToken);
            var userInfo = await _client.GetUserInfoAsync(accessToken.Data?.AccessToken ?? string.Empty, cancellationToken);
            var data = userInfo.Data ?? throw new InvalidOperationException("未能获取飞书用户信息");

            return new IntegrationAuthResult
            {
                IntegrationType = Type,
                OpenId = data.OpenId,
                UnionId = data.UnionId,
                DisplayName = data.Name,
                Avatar = data.AvatarUrl,
                TenantId = setting.TenantId
            };
        }

        public string BuildAuthorizationUrl(IntegrationLoginSetting setting, string state)
        {
            return $"https://accounts.feishu.cn/open-apis/authen/v1/authorize?app_id={Encode(GetClientId(setting))}&redirect_uri={Encode(setting.RedirectUri)}&state={Encode(state)}";
        }
    }
}
