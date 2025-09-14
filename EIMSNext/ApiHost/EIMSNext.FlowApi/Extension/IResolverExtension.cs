using HKH.Mef2.Integration;

using EIMSNext.FlowApi.Authorization;

namespace EIMSNext.FlowApi.Extension
{
    public static class IResolverExtension
    {
        public static IIdentityContext GetIdentityContext(this IResolver resolver)
        {
            return resolver.Resolve<IIdentityContext>();
        }        
    }
}
