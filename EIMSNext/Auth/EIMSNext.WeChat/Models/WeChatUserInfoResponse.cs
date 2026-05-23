using System.Text.Json.Serialization;

namespace EIMSNext.WeChat.Models
{
    public sealed class WeChatUserInfoResponse
    {
        [JsonPropertyName("openid")]
        public string OpenId { get; set; } = string.Empty;

        [JsonPropertyName("unionid")]
        public string UnionId { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string NickName { get; set; } = string.Empty;

        [JsonPropertyName("headimgurl")]
        public string HeadImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("errcode")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
