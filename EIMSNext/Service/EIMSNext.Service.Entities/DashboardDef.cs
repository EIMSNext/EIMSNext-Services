using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 自定义仪表盘
    /// </summary>
    public class DashboardDef : CorpEntityBase
    {
        /// <summary>
        /// 模板Id, 对于从模板安装的仪表盘
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 仪表盘名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 布局
        /// </summary>
        public string Layout { get; set; } = string.Empty;

        /// <summary>
        /// 是否开启全屏自动刷新
        /// </summary>
        public bool AutoRefreshEnabled { get; set; }

        /// <summary>
        /// 自动刷新间隔（分钟）
        /// </summary>
        public int AutoRefreshIntervalMinutes { get; set; } = 15;

        /// <summary>
        /// 是否启用成员发布
        /// </summary>
        public bool MemberPublishEnabled { get; set; }

        /// <summary>
        /// 仪表盘发布成员范围
        /// </summary>
        public List<Member> PublishMembers { get; set; } = [];

    }
}
