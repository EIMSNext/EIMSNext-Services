using HKH.Mef2.Integration;

namespace EIMSNext.ApiService.Extension
{
    public static class IResolverExtension
    {
        public static IIdentityContext GetIdentityContext(this IResolver resolver)
        {
            return resolver.Resolve<IIdentityContext>();
        }
    }
}
