using HKH.Mef2.Integration;

using EIMSNext.Common;
using EIMSNext.Core.Entity;

namespace EIMSNext.ApiService.Extension
{
    public static class IResolverExtension
    {
        public static IIdentityContext GetIdentityContext(this IResolver resolver)
        {
            return resolver.Resolve<IIdentityContext>();
        }

        public static IApiService<T, Q> GetApiService<T, Q>(this IResolver resolver)
            where T : IMongoEntity
            where Q : T, new()
        {
            return resolver.Resolve<IApiService<T, Q>>();
        }
    }
}
