namespace EIMSNext.Auth.Integrations.Abstractions
{
    /// <summary>
    /// 第三方平台换取到的统一身份结果。
    /// </summary>
    public sealed class IntegrationAuthResult
    {
        /// <summary>
        /// 集成类型。
        /// </summary>
        public string IntegrationType { get; init; } = string.Empty;

        /// <summary>
        /// 第三方 OpenId。
        /// </summary>
        public string OpenId { get; init; } = string.Empty;

        /// <summary>
        /// 第三方 UnionId。
        /// </summary>
        public string UnionId { get; init; } = string.Empty;

        /// <summary>
        /// 第三方外部用户标识。
        /// </summary>
        public string ExternalUserId { get; init; } = string.Empty;

        /// <summary>
        /// 企业标识。
        /// </summary>
        public string CorpId { get; init; } = string.Empty;

        /// <summary>
        /// 租户标识。
        /// </summary>
        public string TenantId { get; init; } = string.Empty;

        /// <summary>
        /// 展示名称。
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// 头像地址。
        /// </summary>
        public string Avatar { get; init; } = string.Empty;
    }
}
