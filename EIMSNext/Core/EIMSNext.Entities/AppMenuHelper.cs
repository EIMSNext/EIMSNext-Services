namespace EIMSNext.Entities
{
    /// <summary>
    /// 应用菜单（AppMenu）树辅助方法。
    /// 负责在 <see cref="Entities.AppMenu"/> 树形结构上做查找、删除、扁平化、排序与合法性校验。
    /// </summary>
    public static class AppMenuHelper
    {
        /// <summary>
        /// 在菜单树中按 <see cref="Entities.AppMenu.MenuId"/> 递归查找第一个匹配项。
        /// </summary>
        /// <param name="menus">待搜索的菜单列表（可为 null，视为空）。</param>
        /// <param name="menuId">要查找的菜单 ID。</param>
        /// <returns>找到的菜单；未找到时返回 null。</returns>
        public static Entities.AppMenu? FindMenu(List<Entities.AppMenu> menus, string menuId)
        {
            foreach (var menu in menus)
            {
                if (menu.MenuId == menuId)
                {
                    return menu;
                }

                if (menu.SubMenus?.Count > 0)
                {
                    var matched = FindMenu(menu.SubMenus, menuId);
                    if (matched != null)
                    {
                        return matched;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 从菜单树中按 <see cref="Entities.AppMenu.MenuId"/> 递归删除第一个匹配项。
        /// </summary>
        /// <param name="menus">待修改的菜单列表（会被原地修改）。</param>
        /// <param name="menuId">要删除的菜单 ID。</param>
        /// <returns>是否成功删除（任一层级找到并删除即返回 true）。</returns>
        public static bool RemoveMenu(List<Entities.AppMenu> menus, string menuId)
        {
            var removed = menus.RemoveAll(x => x.MenuId == menuId) > 0;
            if (removed)
            {
                return true;
            }

            foreach (var menu in menus)
            {
                if (menu.SubMenus?.Count > 0 && RemoveMenu(menu.SubMenus, menuId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 深度优先遍历菜单树，将所有菜单（含中间分组）按遍历顺序平铺输出。
        /// </summary>
        /// <param name="menus">待平铺的菜单树。</param>
        /// <returns>菜单的迭代序列，父在子前。</returns>
        public static IEnumerable<Entities.AppMenu> Flatten(IEnumerable<Entities.AppMenu> menus)
        {
            foreach (var menu in menus)
            {
                yield return menu;

                if (menu.SubMenus?.Count > 0)
                {
                    foreach (var child in Flatten(menu.SubMenus))
                    {
                        yield return child;
                    }
                }
            }
        }

        /// <summary>
        /// 对菜单树做规范化：按当前顺序重新计算 <see cref="Entities.AppMenu.SortIndex"/>（(i+1)*100）；
        /// 非分组节点的 <c>SubMenus</c> 设为 null；分组节点的 <c>SubMenus</c> 至少为 []。
        /// 原地修改输入。
        /// </summary>
        /// <param name="menus">待规范化的菜单列表。</param>
        /// <returns>规范化后的同一列表（便于链式调用）。</returns>
        public static List<Entities.AppMenu> Normalize(List<Entities.AppMenu> menus)
        {
            for (var i = 0; i < menus.Count; i++)
            {
                var menu = menus[i];
                menu.SortIndex = (i + 1) * 100;

                if (menu.MenuType == Entities.FormType.Group)
                {
                    menu.SubMenus ??= [];
                    Normalize(menu.SubMenus);
                }
                else
                {
                    menu.SubMenus = null;
                }
            }

            return menus;
        }

        /// <summary>
        /// 校验菜单树结构合法性：分组节点必须包含子菜单且子菜单不能再是分组；叶子节点不能有子菜单。
        /// </summary>
        /// <param name="menus">待校验的菜单树。</param>
        /// <returns>合法返回 true；任意一处违规返回 false。</returns>
        public static bool ValidateTree(IEnumerable<Entities.AppMenu> menus)
        {
            foreach (var menu in menus)
            {
                if (menu.MenuType == Entities.FormType.Group)
                {
                    if (menu.SubMenus == null)
                    {
                        continue;
                    }

                    if (menu.SubMenus.Any(x => x.MenuType == Entities.FormType.Group))
                    {
                        return false;
                    }

                    if (!ValidateTree(menu.SubMenus))
                    {
                        return false;
                    }
                }
                else if (menu.SubMenus?.Count > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
