namespace EIMSNext.ApiService.Extension
{
    public static class IdentityExtension
    {
        public static bool HasOwnerPermission(this IIdentityContext context)
        {
            return context.IdentityType == IdentityType.CorpOwmer;
        }
        public static bool HasCorpAdminPermission(this IIdentityContext context)
        {
            return HasOwnerPermission(context) || context.IdentityType == IdentityType.CorpAdmin;
        }
        //public static bool HasCurrentAppAdminPermission(this IIdentityContext context)
        //{
        //    return HasCorpAdminPermission(context) || context.IdentityType == IdentityType.AppAdmin;
        //}
        //public static bool HasCurrentFormAdminPermission(this IIdentityContext context)
        //{
        //    return context.IdentityType == IdentityType.CorpOwmer;
        //}
        //public static bool HasEmployeePermission(this IIdentityContext context)
        //{
        //    return context.IdentityType == IdentityType.CorpOwmer;
        //}

        //bool IsEmployee { get; }
        ////bool IsSystem { get; }
        //bool IsAnonymous { get; }
    }
}
