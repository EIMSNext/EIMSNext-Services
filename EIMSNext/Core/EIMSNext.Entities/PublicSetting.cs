using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Entities
{
    /// <summary>
    /// 公开发布设置。
    /// 一个 corp 对每个 (AppId, TargetId) 配置一组公开访问控制（链接、二维码、嵌入、外部链接、字段权限等）。
    /// </summary>
    public class PublicSetting : CorpEntityBase
    {
        /// <summary>所属应用 ID。</summary>
        public string AppId { get; set; } = "";

        /// <summary>公开资源类型：表单或仪表盘。</summary>
        public PublicTargetType TargetType { get; set; } = PublicTargetType.Form;

        /// <summary>公开资源 ID（表单 ID 或仪表盘 ID）。</summary>
        public string TargetId { get; set; } = "";

        /// <summary>表单公开发布设置（仅当 <see cref="TargetType"/> = Form 时使用）。</summary>
        public PublicFormSetting Form { get; set; } = new();

        /// <summary>仪表盘公开发布设置（仅当 <see cref="TargetType"/> = Dashboard 时使用）。</summary>
        public PublicDashboardSetting Dashboard { get; set; } = new();
    }

    /// <summary>公开目标的资源类型。</summary>
    public enum PublicTargetType
    {
        /// <summary>表单。</summary>
        Form = 0,

        /// <summary>仪表盘。</summary>
        Dashboard = 1,
    }

    /// <summary>
    /// 仪表盘公开访问设置。
    /// 包含通用的启用 / 有效期 / 访问码控制，详见 <see cref="PublicPublishSection"/>。
    /// </summary>
    public class PublicDashboardSetting : PublicPublishSection
    {
    }

    /// <summary>
    /// 表单公开访问设置。
    /// 包含三种链接各自的子设置：填写链接（FormLink）、数据查询链接（DataLink）、数据查询列表（QueryLink）。
    /// </summary>
    public class PublicFormSetting
    {
        /// <summary>填写链接设置（用户提交数据用）。</summary>
        public PublicFormLinkSetting FormLink { get; set; } = new();

        /// <summary>数据链接设置（公开单条数据详情）。</summary>
        public PublicDataLinkSetting DataLink { get; set; } = new();

        /// <summary>查询链接设置（公开数据列表）。</summary>
        public PublicQueryLinkSetting QueryLink { get; set; } = new();
    }

    /// <summary>
    /// 表单填写链接设置。
    /// 在 <see cref="PublicPublishSection"/> 基础上扩展微信采集、外部链接、是否仅提交一次、是否仅能看/改自己提交的数据。
    /// </summary>
    public class PublicFormLinkSetting : PublicPublishSection
    {
        /// <summary>微信采集设置（通过微信打开链接时强制采集 openid）。</summary>
        public PublicWechatSetting Wechat { get; set; } = new();

        /// <summary>外部链接设置（嵌入到第三方页面）。</summary>
        public PublicExtLinkSetting ExtLink { get; set; } = new();

        /// <summary>是否仅允许每个访问者提交一次。</summary>
        public bool OneSubmit { get; set; }

        /// <summary>访问者是否只能查看自己提交的数据（仅当FormLink> 同时启用时有效）。</summary>
        public bool ViewOwnData { get; set; }

        /// <summary>访问者是否能修改自己提交的数据。</summary>
        public bool EditOwnData { get; set; }
    }

    /// <summary>
    /// 数据详情链接设置。
    /// 在 <see cref="PublicPublishSection"/> 基础上扩展字段级可见/可编辑权限。
    /// </summary>
    public class PublicDataLinkSetting : PublicPublishSection
    {
        /// <summary>字段级权限配置（控制哪些字段对访问者可见/可编辑）。</summary>
        public List<PublicFormFieldPermissionission> Fields { get; set; } = [];
    }

    /// <summary>
    /// 数据查询列表链接设置。
    /// 在 <see cref="PublicPublishSection"/> 基础上指定查询条件和展示字段。
    /// </summary>
    public class PublicQueryLinkSetting : PublicPublishSection
    {
        /// <summary>列表查询条件使用的字段（与表单字段定义一致）。</summary>
        public List<string> QueryFields { get; set; } = [];

        /// <summary>列表展示的字段 ID 列表。</summary>
        public List<string> DisplayFields { get; set; } = [];
    }

    /// <summary>
    /// 公开访问 section 的通用基类（启用 / 有效期 / 访问码）。
    /// </summary>
    public class PublicPublishSection
    {
        /// <summary>是否启用此 section。</summary>
        public bool Enabled { get; set; }

        /// <summary>有效期截止时间（Unix 毫秒）；null 或 0 表示永久有效。</summary>
        public long? ExpireTime { get; set; }

        /// <summary>是否启用访问码（启用后访问者需要输入访问码才能查看）。</summary>
        public bool AccessCodeEnabled { get; set; }

        /// <summary>访问码的 SHA-256 哈希；明文不存储。</summary>
        public string AccessCodeHash { get; set; } = "";
    }

    /// <summary>
    /// 微信采集设置。
    /// </summary>
    public class PublicWechatSetting
    {
        /// <summary>是否启用微信采集。</summary>
        public bool Enabled { get; set; }

        /// <summary>openid 采集方式：静默采集或显式授权。</summary>
        public PublicWechatAcquireMode AcquireMode { get; set; } = PublicWechatAcquireMode.SilentOpenId;
    }

    /// <summary>微信 openid 采集方式。</summary>
    public enum PublicWechatAcquireMode
    {
        /// <summary>用户无感知，静默采集 openid。</summary>
        SilentOpenId = 0,

        /// <summary>需要用户显式点击授权按钮后采集。</summary>
        ExplicitGrant = 1,
    }

    /// <summary>
    /// 外部链接 / 嵌入设置。
    /// </summary>
    public class PublicExtLinkSetting
    {
        /// <summary>是否启用外部链接 / 嵌入。</summary>
        public bool Enabled { get; set; }

        /// <summary>允许嵌入的来源域白名单（Origin）；空表示不限制。</summary>
        public List<string> Values { get; set; } = [];
    }

    /// <summary>
    /// 数据链接的字段级权限。
    /// </summary>
    public class PublicFormFieldPermissionission
    {
        /// <summary>字段 ID（与表单字段定义一致）。</summary>
        public string Field { get; set; } = "";

        /// <summary>访问者是否能看到此字段。</summary>
        public bool Visible { get; set; } = true;

        /// <summary>访问者是否能编辑此字段（要求 <see cref="Visible"/> = true）。</summary>
        public bool Editable { get; set; }
    }
}
