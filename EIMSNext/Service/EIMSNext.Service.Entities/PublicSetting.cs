using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 公开发布设置。
    /// </summary>
    public class PublicSetting : CorpEntityBase
    {
        /// <summary>
        /// 应用ID。
        /// </summary>
        public string AppId { get; set; } = "";

        /// <summary>
        /// 公开资源类型。
        /// </summary>
        public PublicTargetType TargetType { get; set; } = PublicTargetType.Form;

        /// <summary>
        /// 公开资源ID，表单ID或仪表盘ID。
        /// </summary>
        public string TargetId { get; set; } = "";

        /// <summary>
        /// 表单公开发布设置。
        /// </summary>
        public PublicFormSetting Form { get; set; } = new();

        /// <summary>
        /// 仪表盘公开发布设置。
        /// </summary>
        public PublicDashboardSetting Dashboard { get; set; } = new();
    }

    public enum PublicTargetType
    {
        Form = 0,
        Dashboard = 1,
    }

    public class PublicDashboardSetting : PublicPublishSection
    {
    }

    public class PublicFormSetting
    {
        public PublicFormLinkSetting FormLink { get; set; } = new();

        public PublicDataLinkSetting DataLink { get; set; } = new();

        public PublicQueryLinkSetting QueryLink { get; set; } = new();
    }

    public class PublicFormLinkSetting : PublicPublishSection
    {
        public PublicWechatSetting Wechat { get; set; } = new();

        public PublicExtLinkSetting ExtLink { get; set; } = new();

        public bool OneSubmit { get; set; }

        public bool ViewOwnData { get; set; }

        public bool EditOwnData { get; set; }
    }

    public class PublicDataLinkSetting : PublicPublishSection
    {
        public List<PublicFieldPermission> Fields { get; set; } = [];
    }

    public class PublicQueryLinkSetting : PublicPublishSection
    {
        public List<string> QueryFields { get; set; } = [];

        public List<string> DisplayFields { get; set; } = [];
    }

    public class PublicPublishSection
    {
        public bool Enabled { get; set; }

        /// <summary>
        /// 有效期，Unix 毫秒；空表示永久有效。
        /// </summary>
        public long? ExpireTime { get; set; }

        public bool AccessCodeEnabled { get; set; }

        public string AccessCodeHash { get; set; } = "";
    }

    public class PublicWechatSetting
    {
        public bool Enabled { get; set; }

        public PublicWechatAcquireMode AcquireMode { get; set; } = PublicWechatAcquireMode.SilentOpenId;
    }

    public enum PublicWechatAcquireMode
    {
        SilentOpenId = 0,
        ExplicitGrant = 1,
    }

    public class PublicExtLinkSetting
    {
        public bool Enabled { get; set; }

        public List<string> Values { get; set; } = [];
    }

    public class PublicFieldPermission
    {
        public string Field { get; set; } = "";

        public bool Visible { get; set; } = true;

        public bool Editable { get; set; }
    }
}
