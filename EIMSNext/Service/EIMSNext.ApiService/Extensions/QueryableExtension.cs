using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.ApiService.Extensions
{
    /// <summary>
    /// 查询扩展方法。
    /// </summary>
    public static class QueryableExtension
    {
        static readonly Type ICorpOwnedType = typeof(ICorpOwned);
        /// <summary>
        /// 按企业 ID 过滤查询。
        /// </summary>
        public static IQueryable<T> FilterByCorpId<T>(this IQueryable<T> query, string corpId) where T : IMongoEntity
        {
            if (ICorpOwnedType.IsAssignableFrom(typeof(T)))
                return query.Where(x => (x as ICorpOwned)!.CorpId == corpId);
            else
                return query;
        }
    }
}
