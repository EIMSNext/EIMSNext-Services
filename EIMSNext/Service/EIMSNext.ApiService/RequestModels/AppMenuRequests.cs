using EIMSNext.Entities;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 编辑应用菜单请求。
    /// </summary>
    public class EditAppMenuRequest
    {
        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>菜单 ID。</summary>
        public string MenuId { get; set; } = string.Empty;

        /// <summary>菜单名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>图标。</summary>
        public string? Icon { get; set; }

        /// <summary>图标颜色。</summary>
        public string? IconColor { get; set; }
    }

    /// <summary>
    /// 编辑应用分组请求。
    /// </summary>
    public class EditAppGroupRequest
    {
        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>菜单 ID。</summary>
        public string MenuId { get; set; } = string.Empty;

        /// <summary>分组名称。</summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 创建应用分组请求。
    /// </summary>
    public class CreateAppGroupRequest
    {
        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>分组名称。</summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 删除应用分组请求。
    /// </summary>
    public class DeleteAppGroupRequest
    {
        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>菜单 ID。</summary>
        public string MenuId { get; set; } = string.Empty;
    }

    /// <summary>
    /// 保存应用菜单请求。
    /// </summary>
    public class SaveAppMenusRequest
    {
        /// <summary>应用 ID。</summary>
        public string AppId { get; set; } = string.Empty;

        /// <summary>应用菜单列表。</summary>
        public List<AppMenu> AppMenus { get; set; } = [];
    }
}
