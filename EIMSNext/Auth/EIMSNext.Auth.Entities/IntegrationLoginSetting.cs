using EIMSNext.Core.Entities;

namespace EIMSNext.Auth.Entities
{
    /// <summary>
    /// 第三方集成登录配置。
    /// </summary>
    public class IntegrationLoginSetting : MongoEntityBase
    {
        /// <summary>
        /// 集成类型。
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称。
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 应用标识。
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// 应用密钥。
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// 回调地址。
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>
        /// 平台应用标识。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 平台应用密钥。
        /// </summary>
        public string AppSecret { get; set; } = string.Empty;

        /// <summary>
        /// 企业或租户标识。
        /// </summary>
        public string CorpId { get; set; } = string.Empty;

        /// <summary>
        /// AgentId 或类似平台应用代理标识。
        /// </summary>
        public string AgentId { get; set; } = string.Empty;

        /// <summary>
        /// 租户标识。
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// 额外参数集合。
        /// </summary>
        public IList<IntegrationLoginSettingItem> ExtraParameters { get; set; } = new List<IntegrationLoginSettingItem>();
    }

    /// <summary>
    /// 第三方集成登录扩展参数项。
    /// </summary>
    public class IntegrationLoginSettingItem
    {
        /// <summary>
        /// 参数名称。
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 参数值。
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}
