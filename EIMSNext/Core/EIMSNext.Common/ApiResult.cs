using System.Collections;
using System.Text.Json.Serialization;

namespace EIMSNext.Common
{
    public class ApiResult
    {
        private ApiResult(int code, string message, dynamic? data = null)
        {
            Code = code;
            Message = message;
            Value = data;
        }
        [JsonPropertyName("code")]
        public int? Code { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("value")]
        public dynamic? Value { get; set; }

        public static ApiResult Success(dynamic? data = null)
        {
            return new ApiResult(0, "success", data);
        }

        public static ApiResult Fail(int code, string error, dynamic? data = null)
        {
            return new ApiResult(code, string.IsNullOrEmpty(error) ? "fail" : error, data);
        }
    }
}
