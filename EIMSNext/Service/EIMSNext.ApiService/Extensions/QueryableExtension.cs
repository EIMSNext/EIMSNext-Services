using EIMSNext.Core.Entities;

namespace EIMSNext.ApiService.Extensions
{
    public static class QueryableExtension
    {
        static readonly Type ICorpOwnedType = typeof(ICorpOwned);
        public static IQueryable<T> FilterByCorpId<T>(this IQueryable<T> query, string corpId) where T : IMongoEntity
        {
            if (ICorpOwnedType.IsAssignableFrom(typeof(T)))
                return query.Where(x => (x as ICorpOwned)!.CorpId == corpId);
            else
                return query;
        }
    }
}
