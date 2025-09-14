using System.Linq.Expressions;

using EIMSNext.Core.Entity;
using EIMSNext.Core.MongoDb;
using EIMSNext.Core.Query;
using EIMSNext.MongoDb;

using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Core.Repository
{
    public interface IRepository<T> where T : IMongoEntity
    {
        IMongoDbContex DbContext { get; }
        IMongoCollection<T> Collection { get; }
        IQueryable<T> Queryable { get; }
        FilterDefinitionBuilder<T> FilterBuilder { get; }
        SortDefinitionBuilder<T> SortBuilder { get; }
        SearchDefinitionBuilder<T> SearchBuilder { get; }
        ProjectionDefinitionBuilder<T> ProjectionBuilder { get; }
        UpdateDefinitionBuilder<T> UpdateBuilder { get; }

        MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null);

        IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null);
        IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null);
        IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null);
        Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null);
        Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null);
        Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null);
        T? Get(string id, IClientSessionHandle? session = null);
        Task<T?> GetAsync(string id, IClientSessionHandle? session = null);

        long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null);
        long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null);
        long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null);
        Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null);
        Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null);
        Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null);

        void Insert(T entity, IClientSessionHandle? session = null);
        void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null);
        Task InsertAsync(T entity, IClientSessionHandle? session = null);
        Task InsertAsync(IEnumerable<T> entities, IClientSessionHandle? session = null);

        UpdateResult Update(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);
        Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);
        UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);
        Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);
        UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);
        Task<UpdateResult> UpdateManyAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null);

        public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null);
        Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null);

        DeleteResult Delete(string id, IClientSessionHandle? session = null);
        DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null);
        DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null);
        DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null);
        Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null);
        Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null);
        Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null);
        Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null);

        IEnumerable<T> EnsureId(IEnumerable<T> entities);
        T EnsureId(T entity);
        string NewId();
    }
}
