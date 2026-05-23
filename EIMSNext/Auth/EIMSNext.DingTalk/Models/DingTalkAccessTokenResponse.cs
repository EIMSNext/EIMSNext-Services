using System.Text.Json.Serialization;

namespace EIMSNext.DingTalk.Models
{
    public sealed class DingTalkAccessTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expireIn")]
        public int ExpireIn { get; set; }
    }
}
