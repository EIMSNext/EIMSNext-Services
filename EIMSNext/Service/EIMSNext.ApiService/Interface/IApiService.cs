using System.Linq.Expressions;

using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;

using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public interface IApiService<T, V>
        where T : IMongoEntity
        where V : T, new()
    {
        V? Get(string id);

        IQueryable<V> All();

        IQueryable<V> Query(Expression<Func<V, bool>> where);

        IFindFluent<T, T> Find(DynamicFindOptions<T> options);

        IFindFluent<T, T> Find(Expression<Func<T, bool>> filter);

        long Count(DynamicFilter filter);

        long Count(Expression<Func<T, bool>> filter);

        bool Exists(Expression<Func<T, bool>> where);

        bool Exists(DynamicFilter where);

        Task<T?> GetAsync(string id);

        Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options);

        Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter);

        Task<long> CountAsync(DynamicFilter filter);

        Task<long> CountAsync(Expression<Func<T, bool>> filter);

        Task<bool> ExistsAsync(Expression<Func<T, bool>> where);

        Task<bool> ExistsAsync(DynamicFilter where);

        Task AddAsync(T entity);

        Task<ReplaceOneResult> ReplaceAsync(T entity);

        Task<object> DeleteAsync(string id);

        Task<object> DeleteAsync(IEnumerable<string> ids);

        Task<object> DeleteAsync(DynamicFilter filter);
    }
}
