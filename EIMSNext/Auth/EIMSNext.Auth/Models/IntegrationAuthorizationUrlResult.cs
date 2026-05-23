namespace EIMSNext.Auth.Models
{
    /// <summary>
    /// 第三方授权地址返回结果。
    /// </summary>
    public sealed class IntegrationAuthorizationUrlResult
    {
        /// <summary>
        /// 集成类型。
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// 授权跳转地址。
        /// </summary>
        public string AuthorizationUrl { get; init; } = string.Empty;

        /// <summary>
        /// 展示名称。
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;
    }
}
