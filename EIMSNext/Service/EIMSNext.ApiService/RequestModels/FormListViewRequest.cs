using EIMSNext.Service.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 表单视图配置请求。
    /// </summary>
    public class FormListViewRequest : RequestBase
    {
        public string AppId { get; set; } = string.Empty;

        public string FormId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public FormListViewType PcType { get; set; } = FormListViewType.Table;

        public MobileFormListViewType MobileType { get; set; } = MobileFormListViewType.Table;

        public int SortIndex { get; set; }

        public List<string> AuthGroupIds { get; set; } = new List<string>();

        public string Settings { get; set; } = string.Empty;

        public string? DefaultFilter { get; set; }

        public string? DefaultSort { get; set; }

        public bool Disabled { get; set; }
    }
}
