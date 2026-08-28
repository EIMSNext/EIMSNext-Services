using System.Text.Json.Serialization;

namespace EIMSNext.Feishu.Models
{
    public sealed class FeishuAccessTokenResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public FeishuAccessTokenData? Data { get; set; }
    }

    public sealed class FeishuAccessTokenData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
    }
}
