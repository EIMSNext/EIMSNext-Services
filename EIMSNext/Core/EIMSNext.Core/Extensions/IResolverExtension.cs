using HKH.Mef2.Integration;

using EIMSNext.Cache;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Repository;
using EIMSNext.Core.Service;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Core
{
    public static class IResolverExtension
    {
        public static ILogger<T> GetLogger<T>(this IResolver resolver)
        {
            return resolver.Resolve<ILogger<T>>();
        }
        public static IRepository<T> GetRepository<T>(this IResolver resolver) where T : IMongoEntity
        {
            return resolver.Resolve<IRepository<T>>();
        }

        public static IService<T> GetService<T>(this IResolver resolver) where T : IMongoEntity
        {
            return resolver.Resolve<IService<T>>();
        }

        public static ICacheClient GetCacheClient(this IResolver resolver)
        {
            return resolver.Resolve<ICacheClient>();
        }

        public static IMemoryCache GetMemoryCache(this IResolver resolver)
        {
            return resolver.Resolve<IMemoryCache>();
        }

        public static IServiceContext GetServiceContext(this IResolver resolver)
        {
            return resolver.Resolve<IServiceContext>();
        }
    }
}
