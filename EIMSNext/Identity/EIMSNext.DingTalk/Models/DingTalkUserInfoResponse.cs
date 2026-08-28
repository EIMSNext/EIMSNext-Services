using System.Text.Json.Serialization;

namespace EIMSNext.DingTalk.Models
{
    public sealed class DingTalkUserInfoResponse
    {
        [JsonPropertyName("nick")]
        public string Nick { get; set; } = string.Empty;

        [JsonPropertyName("avatarUrl")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonPropertyName("openId")]
        public string OpenId { get; set; } = string.Empty;

        [JsonPropertyName("unionId")]
        public string UnionId { get; set; } = string.Empty;

        [JsonPropertyName("mobile")]
        public string Mobile { get; set; } = string.Empty;
    }
}
