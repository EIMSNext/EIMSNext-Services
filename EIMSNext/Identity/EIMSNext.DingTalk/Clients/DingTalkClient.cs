using System.Text.Json;
using EIMSNext.DingTalk.Models;
using RestSharp;

namespace EIMSNext.DingTalk.Clients
{
    public sealed class DingTalkClient
    {
        private static readonly RestClient Client = new("https://api.dingtalk.com/");

        public async Task<DingTalkAccessTokenResponse> GetUserAccessTokenAsync(string clientId, string clientSecret, string code, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("v1.0/oauth2/userAccessToken", Method.Post)
                .AddJsonBody(new
                {
                    clientId,
                    clientSecret,
                    code,
                    grantType = "authorization_code"
                });

            var response = await Client.ExecuteAsync<DingTalkAccessTokenResponse>(request, cancellationToken);
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "钉钉访问令牌请求失败");
            }

            return response.Data ?? throw new InvalidOperationException("未能获取钉钉访问令牌");
        }

        public async Task<DingTalkUserInfoResponse> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("v1.0/contact/users/me", Method.Get)
                .AddHeader("x-acs-dingtalk-access-token", accessToken);

            var response = await Client.ExecuteAsync(request, cancellationToken);
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "钉钉用户信息请求失败");
            }

            return JsonSerializer.Deserialize<DingTalkUserInfoResponse>(response.Content ?? string.Empty)
                ?? throw new InvalidOperationException("未能获取钉钉用户信息");
        }
    }
}
