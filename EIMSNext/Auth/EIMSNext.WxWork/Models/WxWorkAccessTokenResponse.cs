using System.Text.Json.Serialization;

namespace EIMSNext.WxWork.Models
{
    public sealed class WxWorkAccessTokenResponse
    {
        [JsonPropertyName("errcode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrorMessage { get; set; } = string.Empty;

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
    }
}
