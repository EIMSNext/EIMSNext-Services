using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Entities
{
    /// <summary>
    /// 表单数据视图配置。
    /// </summary>
    public class FormListView : CorpEntityBase
    {
        /// <summary>
        /// 应用ID。
        /// </summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>
        /// 表单ID。
        /// </summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>
        /// 视图名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// PC端视图类型。
        /// </summary>
        public FormListViewType PcType { get; set; } = FormListViewType.Table;

        /// <summary>
        /// 移动端视图类型。
        /// </summary>
        public MobileFormListViewType MobileType { get; set; } = MobileFormListViewType.Table;

        /// <summary>
        /// 排序值。
        /// </summary>
        public int SortIndex { get; set; }

        /// <summary>
        /// 使用范围内的数据权限组ID。为空时表示全部权限组可用。
        /// </summary>
        public List<string> AuthGroupIds { get; set; } = new List<string>();

        /// <summary>
        /// 视图设置JSON。
        /// </summary>
        public string Settings { get; set; } = string.Empty;

        /// <summary>
        /// 默认筛选条件JSON。
        /// </summary>
        public string? DefaultFilter { get; set; }

        /// <summary>
        /// 默认排序规则JSON。
        /// </summary>
        public string? DefaultSort { get; set; }

        /// <summary>
        /// 是否禁用。
        /// </summary>
        public bool Disabled { get; set; }
    }

    /// <summary>
    /// PC端表单视图类型。
    /// </summary>
    public enum FormListViewType
    {
        /// <summary>
        /// 表格。
        /// </summary>
        Table,
        /// <summary>
        /// 看板。
        /// </summary>
        Kanban,
        /// <summary>
        /// 画廊。
        /// </summary>
        Gallery,
    }

    /// <summary>
    /// 移动端表单视图类型。
    /// </summary>
    public enum MobileFormListViewType
    {
        /// <summary>
        /// 表格。
        /// </summary>
        Table,
        /// <summary>
        /// 卡片。
        /// </summary>
        Card,
    }
}
