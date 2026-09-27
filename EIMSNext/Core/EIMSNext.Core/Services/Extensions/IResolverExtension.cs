using EIMSNext.Cache;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Services;
using EIMSNext.Mef;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EIMSNext.Core.Services.Extensions
{
    /// <summary>
    /// 依赖解析器扩展方法，用于解析各类核心服务与组件。
    /// </summary>
    public static class IResolverExtension
    {
        /// <summary>
        /// 根据导出 ID 解析指定类型的 MEF 导出实例。
        /// </summary>
        /// <typeparam name="T">导出类型。</typeparam>
        /// <param name="resolver">依赖解析器。</param>
        /// <param name="id">导出 ID。</param>
        /// <returns>解析出的导出实例。</returns>
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

        /// <summary>
        /// 解析指定类型的日志记录器。
        /// </summary>
        /// <typeparam name="T">日志类别类型。</typeparam>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>日志记录器。</returns>
        public static ILogger<T> GetLogger<T>(this IResolver resolver)
        {
            return resolver.Resolve<ILogger<T>>();
        }

        /// <summary>
        /// 解析指定实体类型的仓储。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>实体仓储。</returns>
        public static IRepository<T> GetRepository<T>(this IResolver resolver) where T : IMongoEntity
        {
            return resolver.Resolve<IRepository<T>>();
        }

        /// <summary>
        /// 解析指定实体类型的服务。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>实体服务。</returns>
        public static IService<T> GetService<T>(this IResolver resolver) where T : IMongoEntity
        {
            return resolver.Resolve<IService<T>>();
        }

        /// <summary>
        /// 解析指定实体类型的自定义服务。
        /// </summary>
        /// <typeparam name="S">服务实现类型。</typeparam>
        /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>自定义服务实例。</returns>
        public static S GetService<S, T>(this IResolver resolver) where T : IMongoEntity where S : class, IService<T>
        {
            return resolver.Resolve<S>();
        }

        /// <summary>
        /// 解析缓存客户端。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>缓存客户端。</returns>
        public static ICacheClient GetCacheClient(this IResolver resolver)
        {
            return resolver.Resolve<ICacheClient>();
        }

        /// <summary>
        /// 解析内存缓存。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>内存缓存。</returns>
        public static IMemoryCache GetMemoryCache(this IResolver resolver)
        {
            return resolver.Resolve<IMemoryCache>();
        }

        /// <summary>
        /// 解析服务上下文。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>服务上下文。</returns>
        public static IServiceContext GetServiceContext(this IResolver resolver)
        {
            return resolver.Resolve<IServiceContext>();
        }
    }
}
