using EIMSNext.Feishu.Models;
using RestSharp;

namespace EIMSNext.Feishu.Clients
{
    public sealed class FeishuClient
    {
        private static readonly RestClient Client = new("https://open.feishu.cn/");

        public async Task<FeishuAccessTokenResponse> GetUserAccessTokenAsync(string appId, string appSecret, string code, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("open-apis/authen/v1/oidc/access_token", Method.Post)
                .AddJsonBody(new
                {
                    grant_type = "authorization_code",
                    code,
                    app_id = appId,
                    app_secret = appSecret
                });

            var response = await Client.ExecuteAsync<FeishuAccessTokenResponse>(request, cancellationToken);
            var data = response.Data ?? throw new InvalidOperationException("未能获取飞书访问令牌");
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "飞书访问令牌请求失败");
            }

            if (data.Code != 0)
            {
                throw new InvalidOperationException(data.Message);
            }

            return data;
        }

        public async Task<FeishuUserInfoResponse> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("open-apis/authen/v1/user_info", Method.Get)
                .AddHeader("Authorization", $"Bearer {accessToken}");

            var response = await Client.ExecuteAsync<FeishuUserInfoResponse>(request, cancellationToken);
            var data = response.Data ?? throw new InvalidOperationException("未能获取飞书用户信息");
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "飞书用户信息请求失败");
            }

            if (data.Code != 0)
            {
                throw new InvalidOperationException(data.Message);
            }

            return data;
        }
    }
}
