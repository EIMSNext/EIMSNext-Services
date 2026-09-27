using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单视图配置请求。
    /// </summary>
    public class FormListViewRequest : RequestBase
    {
        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>表单 ID。</summary>
        public string FormId { get; set; } = string.Empty;

        /// <summary>视图名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>PC 端视图类型。</summary>
        public FormListViewType PcType { get; set; } = FormListViewType.Table;

        /// <summary>移动端视图类型。</summary>
        public MobileFormListViewType MobileType { get; set; } = MobileFormListViewType.Table;

        /// <summary>排序索引。</summary>
        public int SortIndex { get; set; }

        /// <summary>权限组 ID 列表。</summary>
        public List<string> PermissionGroupIds { get; set; } = new List<string>();

        /// <summary>视图设置。</summary>
        public string Settings { get; set; } = string.Empty;

        /// <summary>默认过滤条件。</summary>
        public string? DefaultFilter { get; set; }

        /// <summary>默认排序。</summary>
        public string? DefaultSort { get; set; }

        /// <summary>是否禁用。</summary>
        public bool Disabled { get; set; }
    }
}
