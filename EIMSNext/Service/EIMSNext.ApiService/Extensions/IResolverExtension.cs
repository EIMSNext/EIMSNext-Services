using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;

using HKH.Mef2.Integration;

namespace EIMSNext.ApiService.Extensions
{
    /// <summary>
    /// 依赖解析器扩展方法，用于解析 API 服务。
    /// </summary>
    public static class IResolverExtension
    {
        /// <summary>
        /// 解析指定实体类型的 API 服务。
        /// </summary>
        /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>API 服务。</returns>
        public static IApiService<T> GetApiService<T>(this IResolver resolver)
            where T : IEntityKey
        {
            return resolver.Resolve<IApiService<T>>();
        }

        /// <summary>
        /// 解析指定实体类型的自定义 API 服务。
        /// </summary>
        /// <typeparam name="S">API 服务实现类型。</typeparam>
        /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
        /// <param name="resolver">依赖解析器。</param>
        /// <returns>自定义 API 服务。</returns>
        public static S GetApiService<S, T>(this IResolver resolver)
            where S : class, IApiService<T>
           where T : IEntityKey
        {
            return resolver.Resolve<S>();
        }
    }
}
