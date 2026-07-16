using EIMSNext.Auth.Entities;
using EIMSNext.Common;
using HKH.Common;
using RestSharp;

namespace EIMSNext.Async.Tasks.SystemTask
{
    public sealed class SystemTaskTokenProvider : ISystemTaskTokenProvider
    {
        private readonly RestClient _client;
        private readonly string _clientSecret;

        public SystemTaskTokenProvider(AppSetting appSetting)
        {
            var tokenEndpoint = appSetting.OAuth.SystemTokenEndPoint
                ?? throw new InvalidOperationException("Missing OAuth:BaseUrl or OAuth:Authority for system task token provider");
            _client = new RestClient(tokenEndpoint);
            _clientSecret = InternalClients.SystemClientSecret;
        }

        public async Task<string> GetAccessTokenAsync(string corpId, string objectType, string objectId, CancellationToken cancellationToken = default)
        {
            var request = new RestRequest(string.Empty, Method.Post);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("application/x-www-form-urlencoded",
                $"grant_type={Uri.EscapeDataString(CustomGrantType.System)}&client_id={Uri.EscapeDataString(InternalClients.SystemClientId)}&client_secret={Uri.EscapeDataString(_clientSecret)}&scope={Uri.EscapeDataString("api.readwrite")}&corp_id={Uri.EscapeDataString(corpId)}&object_type={Uri.EscapeDataString(objectType)}&object_id={Uri.EscapeDataString(objectId)}",
                ParameterType.RequestBody);

            var response = await _client.ExecuteAsync<SystemTokenResponse>(request, cancellationToken);
            if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Data?.access_token))
            {
                throw new UnLogException(response.ErrorMessage ?? "获取系统任务 Token 失败");
            }

            return response.Data.access_token;
        }

        private sealed class SystemTokenResponse
        {
            public string access_token { get; set; } = string.Empty;
        }
    }
}
