namespace EIMSNext.Service
{
    public static class AppMenuHelper
    {
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
