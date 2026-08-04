namespace EIMSNext.Common
{
    public static class UrlSafety
    {
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
