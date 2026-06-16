using EIMSNext.Core.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 工作台收藏和访问记录目标类型。
    /// </summary>
    public static class WorkbenchTargetType
    {
        /// <summary>
        /// 应用。
        /// </summary>
        public const string App = "app";

        /// <summary>
        /// 表单。
        /// </summary>
        public const string Form = "form";

        /// <summary>
        /// 仪表盘。
        /// </summary>
        public const string Dashboard = "dashboard";
    }

    /// <summary>
    /// 用户自定义工作台布局配置。
    /// </summary>
    public class WorkbenchConfig : CorpEntityBase
    {
        /// <summary>
        /// 员工 Id。
        /// </summary>
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary>
        /// 工作台布局 JSON。
        /// </summary>
        public string Layout { get; set; } = string.Empty;

        /// <summary>
        /// 页面样式 JSON。
        /// </summary>
        public string PageStyle { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户工作台收藏项。
    /// </summary>
    public class WorkbenchFavorite : CorpEntityBase
    {
        /// <summary>
        /// 员工 Id。
        /// </summary>
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary>
        /// 收藏目标类型。
        /// </summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>
        /// 收藏目标 Id。
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// 目标所属应用 Id。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 展示标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 展示图标。
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 图标颜色。
        /// </summary>
        public string IconColor { get; set; } = string.Empty;

        /// <summary>
        /// 排序值。
        /// </summary>
        public long SortIndex { get; set; }
    }

    /// <summary>
    /// 用户最近访问记录。
    /// </summary>
    public class WorkbenchRecentVisit : CorpEntityBase
    {
        /// <summary>
        /// 员工 Id。
        /// </summary>
        public string EmployeeId { get; set; } = string.Empty;

        /// <summary>
        /// 访问目标类型。
        /// </summary>
        public string TargetType { get; set; } = string.Empty;

        /// <summary>
        /// 访问目标 Id。
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// 目标所属应用 Id。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 展示标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 展示图标。
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 图标颜色。
        /// </summary>
        public string IconColor { get; set; } = string.Empty;

        /// <summary>
        /// 最近访问时间。
        /// </summary>
        public long LastVisitTime { get; set; }

        /// <summary>
        /// 访问次数。
        /// </summary>
        public int VisitCount { get; set; }
    }
}
