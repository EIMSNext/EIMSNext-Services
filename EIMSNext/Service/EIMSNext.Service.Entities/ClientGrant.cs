using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 客户端授权（开放平台 Client 的资源-动作位掩码、应用范围、IP 白名单）。
    ///
    /// 与 <see cref="AuthGroup"/> 互不相干：
    /// <list type="bullet">
    /// <item><see cref="AuthGroup"/>：用户/角色级别的表单数据权限（DataPerms）。</item>
    /// <item><see cref="ClientGrant"/>：OAuth 客户端级别的 API 资源访问权限（Operation 位掩码）。</item>
    /// </list>
    /// </summary>
    public class ClientGrant : CorpEntityBase
    {
        /// <summary>所属 Client.Id（OAuth 客户端主键）。</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>授权名称（冗余便于检索）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>应用授权范围。"all" = 全部应用；"partial" = 仅限 AppIds。</summary>
        public string AppScope { get; set; } = "all";

        /// <summary>部分应用授权时，所选 AppId 列表。</summary>
        public List<string> AppIds { get; set; } = new();

        /// <summary>接口授权范围。"all" = 全部资源全部动作；"partial" = 仅 ResourceActions。</summary>
        public string ApiScope { get; set; } = "all";

        /// <summary>接口部分授权时，所选资源-动作位掩码列表。</summary>
        public List<ResourceActionGrant> ResourceActions { get; set; } = new();

        /// <summary>IP 白名单（每行一个 IP 或 CIDR，留空表示不限制）。</summary>
        public List<string> IpWhitelist { get; set; } = new();

        /// <summary>授权是否启用。false 时 token 颁发直接失败。</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 单一资源的动作位掩码。
    /// <see cref="Actions"/> 是 <see cref="EIMSNext.Common.Operation"/> 的位掩码数值。
    /// </summary>
    public class ResourceActionGrant
    {
        /// <summary>资源代码，对应 <c>EIMSNext.Service.Host.Authorization.Resources</c> 里的常量。</summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>动作位掩码（Read=1, Add=2, Edit=4, Delete=8, Import=16）。</summary>
        public long Actions { get; set; }
    }
}
