using HKH.Mef2.Integration;

using EIMSNext.Common;

namespace EIMSNext.ApiCore
{
    public static class IResolverExtension
    {
        public static AppSetting GetAppSetting(this IResolver resolver)
        {
            return resolver.Resolve<AppSetting>();
        }
    }
}
