using EIMSNext.ApiHost.Authorization;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiHost.Extension
{
    public static class IResolverExtension
    {
        public static IIdentityContext GetIdentityContext(this IResolver resolver)
        {
            return resolver.Resolve<IIdentityContext>();
        }        
    }
}
