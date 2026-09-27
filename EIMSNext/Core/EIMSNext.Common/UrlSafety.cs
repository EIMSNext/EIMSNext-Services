namespace EIMSNext.Common
{
    /// <summary>
    /// 提供 URL 安全性检查的辅助方法。
    /// </summary>
    public static class UrlSafety
    {
        /// <summary>
        /// 判断 URL 是否包含危险协议（如 javascript、vbscript 或 data:text/html）。
        /// </summary>
        /// <param name="value">需要检查的 URL 值。</param>
        /// <returns>包含危险协议时返回 true，否则返回 false。</returns>
        public static bool HasDangerousProtocol(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = new string(value.Where(static ch => !char.IsControl(ch) && !char.IsWhiteSpace(ch)).ToArray());
            return normalized.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase);
        }
    }
}