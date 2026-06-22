using System.Security.Cryptography;
using System.Text;

namespace EIMSNext.Common
{
    public static class PublicPasswordHelper
    {
        public static string GenerateChallenge(string targetId, string secretKey, long timestampMs)
        {
            var input = $"{targetId}:{timestampMs}";
            var hmac = ComputeHmac(secretKey, input);
            return $"{timestampMs}:{Convert.ToBase64String(hmac)}";
        }

        public static bool ValidateChallenge(string targetId, string secretKey, string password, long windowMs)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var parts = password.Split(':', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var timestamp) || string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Math.Abs(now - timestamp) > windowMs)
            {
                return false;
            }

            var expected = ComputeHmac(secretKey, $"{targetId}:{timestamp}");
            var actualBytes = TryDecodeBase64(parts[1]);
            if (actualBytes == null || actualBytes.Length != expected.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(expected, actualBytes);
        }

        private static byte[] ComputeHmac(string secretKey, string input)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        }

        private static byte[]? TryDecodeBase64(string value)
        {
            try
            {
                return Convert.FromBase64String(value);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
