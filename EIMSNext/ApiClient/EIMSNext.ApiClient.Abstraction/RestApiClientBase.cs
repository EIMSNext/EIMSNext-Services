using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using EIMSNext.Common.Extension;
using HKH.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using RestSharp;

namespace EIMSNext.ApiClient.Abstraction
{
    public abstract class RestApiClientBase<TClient, TSetting> : ApiClientBase<TClient, TSetting>
        where TClient : IApiClient
        where TSetting : RestApiClientSetting, new()
    {
        public RestApiClientBase(IConfiguration config, ILogger<TClient> logger) : base(config, logger)
        {
            Client = new RestClient(Setting!.BaseUrl);
        }

        protected RestClient Client { get; private set; }

        protected virtual T? Get<T>(string url, string? token = null)
        {
            var request = new RestRequest(url, Method.Get);
            BuilderRequest(request, null, token);

            var response = Client.Execute(request);
            return HandleResponse<T>(response).Data;
        }
        protected virtual async Task<RestResponse<T>> GetAsync<T>(string url, string? token = null)
        {
            var request = new RestRequest(url, Method.Get);
            BuilderRequest(request, null, token);

            var response = await Client.ExecuteAsync(request);
            return HandleResponse<T>(response);
        }

        protected virtual RestResponse<T> Post<T>(string url, object? data = null, string? token = null, WebContentType contentType = WebContentType.Json)
        {
            var request = new RestRequest(url, Method.Post);
            BuilderRequest(request, data, token, contentType);

            var response = Client.Execute(request);
            return HandleResponse<T>(response);
        }
        protected virtual async Task<RestResponse<T>> PostAsync<T>(string url, object? data = null, string? token = null, WebContentType contentType = WebContentType.Json)
        {
            var request = new RestRequest(url, Method.Post);
            BuilderRequest(request, data, token, contentType);

            var response = await Client.ExecuteAsync(request);
            return HandleResponse<T>(response);
        }

        protected virtual void BuilderRequest(RestRequest request, object? data, string? token, WebContentType contentType = WebContentType.Json)
        {
            if (!string.IsNullOrEmpty(token))
                request.AddHeader("Authorization", token.StartsWith("Bearer ") ? token : "Bearer " + token);

            if (data != null)
            {
                if (contentType == WebContentType.Json)
                {
                    request.AddHeader("Content-Type", "application/json; charset=utf-8");
                    request.AddParameter("application/json", data, ParameterType.RequestBody);
                }
                else if (contentType == WebContentType.UrlEncoded)
                {
                    request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                    request.AddParameter("application/x-www-form-urlencoded", FormatData(data, contentType), ParameterType.RequestBody);
                }
            }
        }
        protected virtual string FormatData(object data, WebContentType contentType)
        {
            if (data is string) return (string)data;
            var jsonStr = data.SerializeToJson();
            if (contentType == WebContentType.Json)
            {
                return jsonStr;
            }
            else if (contentType == WebContentType.UrlEncoded)
            {
                var jsonObj = JsonNode.Parse(jsonStr)!.AsObject()!;
                var builder = new StringBuilder();
                foreach (var kvp in jsonObj)
                {
                    if (kvp.Value != null)
                        builder.Append($"{UrlEncoder.Default.Encode(kvp.Key)}={UrlEncoder.Default.Encode(kvp.Value.ToString())}&");
                }
                return builder.Remove(builder.Length - 1, 1).ToString();
            }

            return "";
        }

        protected virtual RestResponse<T> HandleResponse<T>(RestResponse response)
        {
            if (!response.IsSuccessful)
            {
                Logger.LogError($"API返回错误：{response.Content}");
                var errMsg = "";
                if (response.ErrorException != null)
                {
                    errMsg = response.ErrorException.Message;
                    Logger.LogError(response.ErrorException, $"API请求异常: {response.Request.Resource}");
                    //throw new UnLogException(response.Content, response.ErrorException);
                }
                else
                {
                    errMsg = response.ErrorMessage;
                    Logger.LogError($"API请求异常:  {response.Request.Resource} -- {response.ErrorMessage}");
                    //throw new UnLogException(response.ErrorMessage);
                }
                throw new UnLogException(errMsg);
            }

            var tResp = RestResponse<T>.FromResponse(response);
            tResp.Data = Client.Serializers.DeserializeContent<T>(response);
            return tResp;
        }
    }

    public enum WebContentType
    {
        None = 0,
        Json = 1,
        UrlEncoded = 2
    }
}
