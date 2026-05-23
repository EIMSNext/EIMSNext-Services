using EIMSNext.Core.Entities;

namespace EIMSNext.Auth.Entities
{
    /// <summary>
    /// 本地用户与第三方身份绑定关系。
    /// </summary>
    public class UserIntegrationBinding : MongoEntityBase
    {
        /// <summary>
        /// 本地用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 集成类型。
        /// </summary>
        public string IntegrationType { get; set; } = string.Empty;

        /// <summary>
        /// 第三方 OpenId。
        /// </summary>
        public string OpenId { get; set; } = string.Empty;

        /// <summary>
        /// 第三方 UnionId。
        /// </summary>
        public string UnionId { get; set; } = string.Empty;

        /// <summary>
        /// 第三方外部用户标识。
        /// </summary>
        public string ExternalUserId { get; set; } = string.Empty;

        /// <summary>
        /// 企业标识。
        /// </summary>
        public string CorpId { get; set; } = string.Empty;

        /// <summary>
        /// 租户标识。
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// 第三方昵称。
        /// </summary>
        public string NickName { get; set; } = string.Empty;

        /// <summary>
        /// 第三方头像地址。
        /// </summary>
        public string Avatar { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
