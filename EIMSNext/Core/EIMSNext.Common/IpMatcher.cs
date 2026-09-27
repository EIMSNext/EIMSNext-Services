using System.Net;
using System.Net.Sockets;

namespace EIMSNext.Common
{
    /// <summary>
    /// 通用 IP 规则匹配器：支持精确匹配、通配符与 CIDR。
    ///
    /// <para>规则格式：</para>
    /// <list type="bullet">
    /// <item>精确 IP：<c>10.0.0.1</c></item>
    /// <item>通配符：<c>10.0.0.*</c>（仅末段支持 <c>*</c>，匹配整段 0-255）</item>
    /// <item>CIDR：<c>10.0.0.0/24</c>（仅 IPv4）</item>
    /// </list>
    /// </summary>
    public static class IpMatcher
    {
        /// <summary>
        /// 判断 <paramref name="clientIp"/> 是否被 <paramref name="rules"/> 任意一条规则放行。
        /// 规则列表为空或 null 视为"不限制"，直接返回 true。
        /// </summary>
        public static bool IsAllowed(string? clientIp, IReadOnlyCollection<string>? rules)
        {
            if (rules == null || rules.Count == 0)
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(clientIp))
            {
                return false;
            }
            foreach (var raw in rules)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }
                var rule = raw.Trim();
                if (Matches(rule, clientIp.Trim()))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断单条规则是否匹配指定 IP。
        /// </summary>
        public static bool Matches(string rule, string clientIp)
        {
            if (string.IsNullOrWhiteSpace(rule) || string.IsNullOrWhiteSpace(clientIp))
            {
                return false;
            }
            rule = rule.Trim();
            clientIp = clientIp.Trim();

            if (string.Equals(rule, clientIp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (rule.Contains('/'))
            {
                return MatchCidr(rule, clientIp);
            }

            if (rule.EndsWith(".*", StringComparison.Ordinal))
            {
                return MatchWildcard(rule, clientIp);
            }

            return false;
        }

        /// <summary>
        /// 判断单条 IP 白名单规则是否为有效的 IPv4、末段通配符或 CIDR 规则。
        /// </summary>
        public static bool IsValidRule(string? rule)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                return false;
            }

            rule = rule.Trim();
            if (IPAddress.TryParse(rule, out var address))
            {
                return address.AddressFamily == AddressFamily.InterNetwork;
            }

            if (rule.EndsWith(".*", StringComparison.Ordinal))
            {
                var parts = rule[..^2].Split('.');
                return parts.Length == 3 && parts.All(IsByte);
            }

            var cidr = rule.Split('/');
            return cidr.Length == 2
                && IPAddress.TryParse(cidr[0], out var network)
                && network.AddressFamily == AddressFamily.InterNetwork
                && int.TryParse(cidr[1], out var prefix)
                && prefix is >= 0 and <= 32;
        }

        private static bool MatchWildcard(string rule, string clientIp)
        {
            var prefix = rule[..^2];
            return clientIp.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchCidr(string rule, string clientIp)
        {
            var parts = rule.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var prefix))
            {
                return false;
            }
            if (prefix < 0 || prefix > 32)
            {
                return false;
            }
            if (!IPAddress.TryParse(parts[0], out var ruleIp) ||
                !IPAddress.TryParse(clientIp, out var cip))
            {
                return false;
            }
            if (ruleIp.AddressFamily != AddressFamily.InterNetwork ||
                cip.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }
            var ruleBytes = ruleIp.GetAddressBytes();
            var cipBytes = cip.GetAddressBytes();
            var fullBytes = prefix / 8;
            var remBits = prefix % 8;
            for (int i = 0; i < fullBytes; i++)
            {
                if (ruleBytes[i] != cipBytes[i])
                {
                    return false;
                }
            }
            if (remBits > 0 && fullBytes < ruleBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - remBits) & 0xFF);
                if ((ruleBytes[fullBytes] & mask) != (cipBytes[fullBytes] & mask))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsByte(string value)
            => byte.TryParse(value, out _);
    }
}
