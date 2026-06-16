using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 仪表盘定义请求
    /// </summary>
    public class DashboardDefRequest : RequestBase
    {
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

        /// <summary>
        /// 是否公开发布
        /// </summary>
        public bool PublicEnabled { get; set; }

        /// <summary>
        /// 公开访问Token
        /// </summary>
        public string PublicToken { get; set; } = string.Empty;
    }
}
