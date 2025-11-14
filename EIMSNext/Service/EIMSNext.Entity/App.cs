using EIMSNext.Core.Entity;

namespace EIMSNext.Entity
{
    /// <summary>
    /// 应用
    /// </summary>
    public class App : CorpEntityBase
    {
        /// <summary>
        /// 模板Id, 对于从模板安装的应用
        /// </summary>
        public string? TemplateId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string Description { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string Icon { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string IconColor { get; set; } = "";
        /// <summary>
        /// 分组
        /// </summary>
        public string? GroupId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int SortIndex { get; set; }

        /// <summary>
        /// 应用菜单
        /// </summary>
        public List<AppMenu> AppMenus { get; set; } = new List<AppMenu>();
    }

    /// <summary>
    /// 应用菜单
    /// </summary>
    public class AppMenu
    {
        /// <summary>
        /// 分组ID或表单ID
        /// </summary>
        public string MenuId { get; set; } = string.Empty;

        /// <summary>
        /// 菜单名称
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; } = "";
        /// <summary>
        /// 
        /// </summary>
        public string IconColor { get; set; } = "";
        /// <summary>
        /// 菜单类型
        /// </summary>
        public FormType MenuType { get; set; }

        /// <summary>
        /// 排序值
        /// </summary>
        public float SortIndex { get; set; }

        /// <summary>
        /// 子菜单
        /// </summary>
        public List<AppMenu>? SubMenus { get; set; }
    }
}
