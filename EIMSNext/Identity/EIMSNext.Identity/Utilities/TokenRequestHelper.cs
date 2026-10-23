using System.Text;
using System.Text.Json;

using OpenIddict.Abstractions;

namespace EIMSNext.Identity.Utilities
{
    public sealed record EncryptedFieldsParseResult(bool Succeeded, Dictionary<string, string>? Fields, string? Error, string? ErrorDescription)
    {
        public static EncryptedFieldsParseResult Success(Dictionary<string, string> fields) => new(true, fields, null, null);

        public static EncryptedFieldsParseResult Failure(string error, string description) => new(false, null, error, description);
    }

    public static class TokenRequestHelper
    {
        /// <summary>
        /// authorizeCode 请求 encrypted 的混淆变体还原：前端把 base64 真实前缀
        /// <c>eyJ</c>（即 <c>{"</c> 的编码）替换为 3 位随机数，并去掉末尾 = 补位。
        /// 还原规则：前 3 位是数字则换回 eyJ；按长度重新补齐 =（长度 %4==1 属非法输入，
        /// 原样返回交由 ParseEncryptedFields 报错）。encrypted 必须是混淆后的变体：
        /// 空/空白或仍是原始 eyJ 前缀（未混淆）的输入一律返回空串，拒绝未混淆请求。
        /// </summary>
        public static string? UnscrambleEncrypted(string? encrypted)
        {
            if (string.IsNullOrWhiteSpace(encrypted) || encrypted.StartsWith("eyJ"))
            {
                return string.Empty;
            }

            var value = encrypted.Trim();
            if (value.Length >= 3 && char.IsAsciiDigit(value[0]) && char.IsAsciiDigit(value[1]) && char.IsAsciiDigit(value[2]))
            {
                value = "eyJ" + value[3..];
            }

            switch (value.Length % 4)
            {
                case 2: return value + "==";
                case 3: return value + "=";
                default: return value;
            }
        }

        public static EncryptedFieldsParseResult ParseEncryptedFields(string? encrypted)
        {
            if (string.IsNullOrWhiteSpace(encrypted))
            {
                return EncryptedFieldsParseResult.Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The encrypted field is required.");
            }

            string json;
            try
            {
                var bytes = Convert.FromBase64String(encrypted);
                json = Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return EncryptedFieldsParseResult.Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The encrypted value is not a valid Base64 string.");
            }

            try
            {
                var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                return EncryptedFieldsParseResult.Success(fields);
            }
            catch (JsonException)
            {
                return EncryptedFieldsParseResult.Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "The encrypted payload is not a valid JSON object.");
            }
        }

        public static OpenIddictRequest CreateRequest(IEnumerable<KeyValuePair<string, string?>> fields)
        {
            return new OpenIddictRequest(fields);
        }
    }
}
