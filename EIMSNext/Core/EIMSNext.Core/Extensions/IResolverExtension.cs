using EIMSNext.Cache;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Repositories;
using EIMSNext.Core.Services;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Core
{
    public static class IResolverExtension
    {
        public static T ResolveExport<T>(this IResolver resolver, string id) where T : class
        {
            var export = resolver.GetExports<Lazy<T, Dictionary<string, object>>>()
            .FirstOrDefault(x => x.Metadata![MefMetadata.Id].ToString() == id)?.Value;

            if (export == null)
            {
                throw new NotSupportedException($"未找到导出: {typeof(T).Name}, id={id}");
            }

            return export;
        }

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

        public static S GetService<S, T>(this IResolver resolver) where T : IMongoEntity where S : class, IService<T>
        {
            return resolver.Resolve<S>();
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
