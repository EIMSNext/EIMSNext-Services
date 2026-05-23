using EIMSNext.WxWork.Models;
using RestSharp;

namespace EIMSNext.WxWork.Clients
{
    public sealed class WxWorkClient
    {
        private static readonly RestClient Client = new("https://qyapi.weixin.qq.com/");

        public async Task<WxWorkAccessTokenResponse> GetAccessTokenAsync(string corpId, string corpSecret, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("cgi-bin/gettoken", Method.Get)
                .AddQueryParameter("corpid", corpId)
                .AddQueryParameter("corpsecret", corpSecret);

            var response = await Client.ExecuteAsync<WxWorkAccessTokenResponse>(request, cancellationToken);
            var data = response.Data ?? throw new InvalidOperationException("未能获取企业微信访问令牌");
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "企业微信访问令牌请求失败");
            }

            if (data.ErrorCode != 0)
            {
                throw new InvalidOperationException(data.ErrorMessage);
            }

            return data;
        }

        public async Task<WxWorkUserInfoResponse> GetUserInfoAsync(string accessToken, string code, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("cgi-bin/auth/getuserinfo", Method.Get)
                .AddQueryParameter("access_token", accessToken)
                .AddQueryParameter("code", code);

            var response = await Client.ExecuteAsync<WxWorkUserInfoResponse>(request, cancellationToken);
            var data = response.Data ?? throw new InvalidOperationException("未能获取企业微信用户身份");
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "企业微信用户身份请求失败");
            }

            if (data.ErrorCode != 0)
            {
                throw new InvalidOperationException(data.ErrorMessage);
            }

            return data;
        }

        public async Task<WxWorkUserDetailResponse> GetUserDetailAsync(string accessToken, string userId, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest("cgi-bin/user/get", Method.Get)
                .AddQueryParameter("access_token", accessToken)
                .AddQueryParameter("userid", userId);

            var response = await Client.ExecuteAsync<WxWorkUserDetailResponse>(request, cancellationToken);
            var data = response.Data ?? throw new InvalidOperationException("未能获取企业微信用户详情");
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(response.ErrorMessage ?? "企业微信用户详情请求失败");
            }

            if (data.ErrorCode != 0)
            {
                throw new InvalidOperationException(data.ErrorMessage);
            }

            return data;
        }
    }
}
