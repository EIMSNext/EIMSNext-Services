namespace EIMSNext.Common
{
    /// <summary>
    /// 提供 <see cref="Operation"/> 权限枚举的位运算扩展方法。
    /// </summary>
    public static class PermissionUtil
    {
        /// <summary>
        /// 判断权限集合是否包含指定权限。
        /// </summary>
        /// <param name="all">权限集合。</param>
        /// <param name="toCheck">需要检查的权限。</param>
        /// <returns>包含指定权限时返回 true，否则返回 false。</returns>
        public static bool HasPermission(this Operation all, Operation toCheck)
        {
            return all.HasFlag(toCheck);
        }

        /// <summary>
        /// 向权限集合添加指定权限。
        /// </summary>
        /// <param name="all">权限集合。</param>
        /// <param name="toAdd">需要添加的权限。</param>
        /// <returns>添加权限后的权限集合。</returns>
        public static Operation AddPermission(this Operation all, Operation toAdd)
        {
            return all | toAdd;
        }

        /// <summary>
        /// 从权限集合移除指定权限。
        /// </summary>
        /// <param name="all">权限集合。</param>
        /// <param name="toRemove">需要移除的权限。</param>
        /// <returns>移除权限后的权限集合。</returns>
        public static Operation RemovePermission(this Operation all, Operation toRemove)
        {
            return all & (~toRemove);
        }
    }
}