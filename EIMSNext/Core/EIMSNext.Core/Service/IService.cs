using System.Linq.Expressions;

using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;

using MongoDB.Driver;

namespace EIMSNext.Core.Service
{
    public interface IService<T> where T : IMongoEntity
    {
        IMongoCollection<T> Collection { get; }

        #region Methods

        T? Get(string id);
        IQueryable<T> All();
        IQueryable<T> Query(Expression<Func<T, bool>> where);
        IFindFluent<T, T> Find(DynamicFindOptions<T> options);
        IFindFluent<T, T> Find(Expression<Func<T, bool>> filter);
        long Count(DynamicFilter filter);
        long Count(Expression<Func<T, bool>> filter);

        bool Exists(Expression<Func<T, bool>> where);
        bool Exists(DynamicFilter where);

        void Add(T entity);
        void Add(IEnumerable<T> entities);
        ReplaceOneResult Replace(T entity);

        object Delete(string id);
        object Delete(IEnumerable<string> ids);
        object Delete(DynamicFilter filter);

        #endregion

        #region Async Methods

        Task<T?> GetAsync(string id);
        Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options);
        Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter);
        Task<long> CountAsync(DynamicFilter filter);
        Task<long> CountAsync(Expression<Func<T, bool>> filter);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> where);
        Task<bool> ExistsAsync(DynamicFilter where);
        Task AddAsync(T entity);
        Task AddAsync(IEnumerable<T> entities);

        Task<ReplaceOneResult> ReplaceAsync(T entity);
        Task<object> DeleteAsync(string id);
        Task<object> DeleteAsync(IEnumerable<string> ids);
        Task<object> DeleteAsync(DynamicFilter filter);

        #endregion
    }
}
