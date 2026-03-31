using EIMSNext.Core.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService.Extensions
{
    public static class IResolverExtension
    {

        public static IApiService<T, Q> GetApiService<T, Q>(this IResolver resolver)
            where T : IMongoEntity
            where Q : T, new()
        {
            return resolver.Resolve<IApiService<T, Q>>();
        }

        public static S GetApiService<S, T, Q>(this IResolver resolver)
            where S : class, IApiService<T, Q>
           where T : IMongoEntity
           where Q : T, new()
        {
            return resolver.Resolve<S>();
        }
    }
}
