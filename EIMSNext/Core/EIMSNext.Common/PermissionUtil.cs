namespace EIMSNext.Common
{
    public static class PermissionUtil
    {
        public static bool HasPermission(this Operation all, Operation toCheck)
        {
            return all.HasFlag(toCheck);
        }

        public static Operation AddPermission(this Operation all, Operation toAdd)
        {
            return all | toAdd;
        }

        public static Operation RemovePermission(this Operation all, Operation toRemove)
        {
            return all & (~toRemove);
        }
    }
}
