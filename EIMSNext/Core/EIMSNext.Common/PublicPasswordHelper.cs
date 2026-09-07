using System.Security.Cryptography;
using System.Text;

namespace EIMSNext.Common
{
    /// <summary>
    /// 提供公共访问密码（基于 HMAC-SHA256 的一次性挑战密码）的生成与校验功能。
    /// </summary>
    public static class PublicPasswordHelper
    {
        /// <summary>
        /// 生成用于公共访问的一次性挑战密码。
        /// </summary>
        /// <param name="targetId">目标资源标识。</param>
        /// <param name="secretKey">用于计算 HMAC 的密钥。</param>
        /// <param name="timestampMs">生成时间戳（Unix 毫秒）。</param>
        /// <returns>形如 "时间戳:Base64签名" 的挑战密码字符串。</returns>
        public static string GenerateChallenge(string targetId, string secretKey, long timestampMs)
        {
            var input = $"{targetId}:{timestampMs}";
            var hmac = ComputeHmac(secretKey, input);
            return $"{timestampMs}:{Convert.ToBase64String(hmac)}";
        }

        /// <summary>
        /// 校验用户提交的公共访问密码是否有效。
        /// </summary>
        /// <param name="targetId">目标资源标识。</param>
        /// <param name="secretKey">用于计算 HMAC 的密钥。</param>
        /// <param name="password">用户提交的挑战密码字符串。</param>
        /// <param name="windowMs">允许的时间窗口（毫秒），超出则视为过期。</param>
        /// <returns>密码有效时返回 true，否则返回 false。</returns>
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