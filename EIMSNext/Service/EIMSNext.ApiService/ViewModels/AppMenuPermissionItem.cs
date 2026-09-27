using EIMSNext.Entities;

namespace EIMSNext.ApiService.ViewModels
{
    /// <summary>
    /// 应用菜单权限项。
    /// </summary>
    public class AppMenuPermissionItem
    {
        /// <summary>
        /// 获取或设置Id。
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置Type。
        /// </summary>
        public FormType Type { get; set; }
    }
}
