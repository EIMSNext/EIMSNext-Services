using System.Text;
using System.Text.Json;

using OpenIddict.Abstractions;

namespace EIMSNext.Auth.Utilities
{
    public sealed record EncryptedFieldsParseResult(bool Succeeded, Dictionary<string, string>? Fields, string? Error, string? ErrorDescription)
    {
        public static EncryptedFieldsParseResult Success(Dictionary<string, string> fields) => new(true, fields, null, null);

        public static EncryptedFieldsParseResult Failure(string error, string description) => new(false, null, error, description);
    }

    public static class TokenRequestHelper
    {
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
