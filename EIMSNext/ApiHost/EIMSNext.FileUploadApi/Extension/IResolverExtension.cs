using EIMSNext.FileUploadApi.Authorization;
using HKH.Mef2.Integration;

namespace EIMSNext.FileUploadApi.Extension
{
    public static class IResolverExtension
    {
        public static IIdentityContext GetIdentityContext(this IResolver resolver)
        {
            return resolver.Resolve<IIdentityContext>();
        }        
    }
}
