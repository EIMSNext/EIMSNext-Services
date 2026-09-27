using System.Collections;
using System.Text.Json.Serialization;

namespace EIMSNext.Common
{
    /// <summary>
    /// 表示 API 的统一响应结果。
    /// </summary>
    public class ApiResult
    {
        private ApiResult(int code, string message, dynamic? data = null)
        {
            Code = code;
            Message = message;
            Value = data;
        }

        /// <summary>
        /// 获取或设置响应状态码，0 表示成功，非 0 表示失败。
        /// </summary>
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        /// <summary>
        /// 获取或设置响应消息。
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// 获取或设置响应数据。
        /// </summary>
        [JsonPropertyName("value")]
        public dynamic? Value { get; set; }

        /// <summary>
        /// 创建成功响应结果。
        /// </summary>
        /// <param name="data">可选的成功响应数据。</param>
        /// <returns>表示成功的结果实例。</returns>
        public static ApiResult Success(dynamic? data = null)
        {
            return new ApiResult(0, "success", data);
        }

        /// <summary>
        /// 创建失败响应结果。
        /// </summary>
        /// <param name="code">失败状态码。</param>
        /// <param name="error">失败消息，为空时使用默认消息 "fail"。</param>
        /// <param name="data">可选的附加数据。</param>
        /// <returns>表示失败的结果实例。</returns>
        public static ApiResult Fail(int code, string error, dynamic? data = null)
        {
            return new ApiResult(code, string.IsNullOrEmpty(error) ? "fail" : error, data);
        }
    }
}