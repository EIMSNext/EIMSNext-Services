using System.Text.Json.Serialization;

namespace EIMSNext.Feishu.Models
{
    public sealed class FeishuUserInfoResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public FeishuUserInfoData? Data { get; set; }
    }

    public sealed class FeishuUserInfoData
    {
        [JsonPropertyName("open_id")]
        public string OpenId { get; set; } = string.Empty;

        [JsonPropertyName("union_id")]
        public string UnionId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;
    }
}
