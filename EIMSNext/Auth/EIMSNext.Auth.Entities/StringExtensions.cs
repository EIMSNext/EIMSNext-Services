using System.Security.Cryptography;
using System.Text;

namespace EIMSNext.Auth.Entities
{
    public static class StringExtensions
    {
        public static string Sha256(this string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
