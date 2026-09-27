using System.Security.Cryptography;
using System.Text;

namespace EIMSNext.Common
{
    /// <summary>
    /// 提供 Webhook 请求签名计算的辅助方法。
    /// </summary>
    public static class WebhookSignature
    {
        /// <summary>
        /// 使用指定密钥计算负载字符串的 HMAC-SHA256 签名。
        /// </summary>
        /// <param name="payload">需要签名的负载字符串。</param>
        /// <param name="secret">签名密钥。</param>
        /// <returns>签名字符串（小写十六进制）。</returns>
        public static string Compute(string payload, string secret)
        {
            return Compute(Encoding.UTF8.GetBytes(payload), secret);
        }

        /// <summary>
        /// 使用指定密钥计算负载字节序列的 HMAC-SHA256 签名。
        /// </summary>
        /// <param name="payload">需要签名的负载字节序列。</param>
        /// <param name="secret">签名密钥。</param>
        /// <returns>签名字符串（小写十六进制）。</returns>
        public static string Compute(ReadOnlySpan<byte> payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(payload.ToArray())).ToLowerInvariant();
        }
    }
}