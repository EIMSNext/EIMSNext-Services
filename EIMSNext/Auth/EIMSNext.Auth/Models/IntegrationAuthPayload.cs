namespace EIMSNext.Auth.Models
{
    /// <summary>
    /// 第三方集成登录请求载荷。
    /// </summary>
    public sealed class IntegrationAuthPayload
    {
        /// <summary>
        /// 原始密码串。
        /// </summary>
        public string Raw { get; init; } = string.Empty;

        /// <summary>
        /// 授权码。
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// 状态值。
        /// </summary>
        public string State { get; init; } = string.Empty;

        /// <summary>
        /// 分段参数。
        /// </summary>
        public IReadOnlyList<string> Parts { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 从集成登录密码串解析载荷。
        /// </summary>
        public static IntegrationAuthPayload Parse(string password)
        {
            var parts = password
                .Split('|', StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            return new IntegrationAuthPayload
            {
                Raw = password,
                Code = parts.ElementAtOrDefault(0) ?? string.Empty,
                State = parts.ElementAtOrDefault(1) ?? string.Empty,
                Parts = parts
            };
        }
    }
}
