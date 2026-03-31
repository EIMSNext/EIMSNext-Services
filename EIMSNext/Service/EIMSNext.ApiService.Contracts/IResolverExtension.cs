using HKH.Mef2.Integration;

namespace EIMSNext.ApiService.Extensions
{
    public static class IResolverExtension
    {
        public static IIdentityContext GetIdentityContext(this IResolver resolver)
        {
            return resolver.Resolve<IIdentityContext>();
        }
    }
}
