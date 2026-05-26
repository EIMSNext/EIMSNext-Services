namespace EIMSNext.Auth.Integrations.Abstractions
{
    /// <summary>
    /// 第三方登录提供器的扩展能力描述。
    /// </summary>
    public sealed class IntegrationProviderCapability
    {
        /// <summary>
        /// 账号未绑定时的提示文案。
        /// </summary>
        public string UnboundFailureMessage { get; init; } = "第三方集成登录失败";

        /// <summary>
        /// 是否允许在未绑定时自动创建用户。
        /// </summary>
        public bool CanAutoProvisionUser { get; init; }

        /// <summary>
        /// 自动创建用户时的默认名称。
        /// </summary>
        public string DefaultUserName { get; init; } = "第三方用户";
    }
}
