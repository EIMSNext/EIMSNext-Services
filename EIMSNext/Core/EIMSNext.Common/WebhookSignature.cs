using System.Security.Cryptography;
using System.Text;

namespace EIMSNext.Common
{
    public static class WebhookSignature
    {
        public static string Compute(string payload, string secret)
        {
            return Compute(Encoding.UTF8.GetBytes(payload), secret);
        }

        public static string Compute(ReadOnlySpan<byte> payload, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(payload.ToArray())).ToLowerInvariant();
        }
    }
}
