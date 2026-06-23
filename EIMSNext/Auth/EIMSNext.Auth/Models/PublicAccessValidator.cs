using EIMSNext.ApiService;
using EIMSNext.Common;
using System.Security.Cryptography;
using System.Text;

namespace EIMSNext.Auth.Models
{
    /// <summary>
    /// 严格按 scope 校验 section 的访问权限。
    /// 不允许跨 section 匹配 accessCode（关键安全边界）。
    /// </summary>
    public static class PublicAccessValidator
    {
        public const long DefaultTimestampWindowMs = 5 * 60 * 1000;

        /// <summary>
        /// 解析给定 scope 对应的 section。
        /// </summary>
        public static PublishSection? ResolveSection(PublicAccessSetting setting, PublicScope scope)
        {
            if (setting == null) return null;
            return scope switch
            {
                PublicScope.FormLink => setting.Form?.FormLink,
                PublicScope.DataLink => setting.Form?.DataLink,
                PublicScope.QueryLink => setting.Form?.QueryLink,
                PublicScope.DashLink => setting.Dashboard,
                _ => null
            };
        }

        /// <summary>
        /// section 是否处于有效启用状态。
        /// </summary>
        public static bool IsSectionEnabled(PublishSection? section, long? nowMs = null)
        {
            if (section == null || !section.Enabled) return false;
            var now = nowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (section.ExpireTime.HasValue && now > section.ExpireTime.Value)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 严格按 scope 校验 access。
        /// - section 为 null 或未启用 → false
        /// - section 启用 access code → 必须匹配 accessCodeHash
        /// - section 未启用 access code → 用 HMAC challenge
        /// </summary>
        public static bool ValidateSection(
            PublishSection? section,
            string? password,
            string targetId,
            string secretKey,
            long timestampWindowMs = DefaultTimestampWindowMs,
            long? nowMs = null)
        {
            if (!IsSectionEnabled(section, nowMs))
            {
                return false;
            }

            if (section!.AccessCodeEnabled)
            {
                if (string.IsNullOrWhiteSpace(section.AccessCodeHash) || string.IsNullOrEmpty(password))
                {
                    return false;
                }

                return FixedTimeEquals(HashAccessCode(password), section.AccessCodeHash);
            }

            return PublicPasswordHelper.ValidateChallenge(targetId, secretKey, password ?? string.Empty, timestampWindowMs);
        }

        private static string HashAccessCode(string accessCode)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(accessCode));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left.Trim().ToLowerInvariant());
            var rightBytes = Encoding.UTF8.GetBytes(right.Trim().ToLowerInvariant());
            return leftBytes.Length == rightBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
    }
}
