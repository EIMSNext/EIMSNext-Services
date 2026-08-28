using EIMSNext.WeChat.Models;
using RestSharp;

namespace EIMSNext.WeChat.Clients
{
    public sealed class WeChatOpenClient
    {
        private static readonly RestClient Client = new("https://api.weixin.qq.com/");

        public async Task<WeChatAccessTokenResponse> ExchangeCodeAsync(string appId, string appSecret, string code, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("sns/oauth2/access_token", Method.Get)
                .AddQueryParameter("appid", appId)
                .AddQueryParameter("secret", appSecret)
                .AddQueryParameter("code", code)
                .AddQueryParameter("grant_type", "authorization_code");

            var response = await Client.ExecuteAsync<WeChatAccessTokenResponse>(request, cancellationToken);
            var data = response.Data ?? throw new InvalidOperationException("未能获取微信令牌响应");
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "微信令牌请求失败");
            }

            if (data.ErrorCode != 0)
            {
                throw new InvalidOperationException(data.ErrorMessage);
            }

            return data;
        }

        public async Task<WeChatUserInfoResponse> GetUserInfoAsync(string accessToken, string openId, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("sns/userinfo", Method.Get)
                .AddQueryParameter("access_token", accessToken)
                .AddQueryParameter("openid", openId)
                .AddQueryParameter("lang", "zh_CN");

            var response = await Client.ExecuteAsync<WeChatUserInfoResponse>(request, cancellationToken);
            var data = response.Data ?? throw new InvalidOperationException("未能获取微信用户信息");
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "微信用户信息请求失败");
            }

            if (data.ErrorCode != 0)
            {
                throw new InvalidOperationException(data.ErrorMessage);
            }

            return data;
        }
    }
}
