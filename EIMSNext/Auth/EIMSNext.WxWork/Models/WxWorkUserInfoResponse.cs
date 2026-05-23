using System.Text.Json.Serialization;

namespace EIMSNext.WxWork.Models
{
    public sealed class WxWorkUserInfoResponse
    {
        [JsonPropertyName("errcode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrorMessage { get; set; } = string.Empty;

        [JsonPropertyName("UserId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("OpenId")]
        public string OpenId { get; set; } = string.Empty;

        [JsonPropertyName("external_userid")]
        public string ExternalUserId { get; set; } = string.Empty;
    }
}
