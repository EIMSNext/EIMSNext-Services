using System.Linq.Expressions;

using HKH.Mef2.Integration;

using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;

using MongoDB.Driver;
using EIMSNext.Common;

namespace EIMSNext.Core.Service
{
    public abstract class MongoEntityServiceBase<T> : ServiceCore<T>, IService<T> where T : class, IMongoEntity
    {
        #region Variables

        #endregion 

        public MongoEntityServiceBase(IResolver resolver)
            : base(resolver)
        {
        }

        #region Properties

        public IMongoCollection<T> Collection => Repository.Collection;

        #endregion

        #region Methods

        public T? Get(string id)
        {
            return GetCore(id, null);
        }

        public IQueryable<T> All()
        {
            return Repository.Queryable;
        }
        public IQueryable<T> Query(Expression<Func<T, bool>> where)
        {
            return Repository.Queryable.Where(where);
        }
        public IFindFluent<T, T> Find(DynamicFindOptions<T> options)
        {
            return FindCore(options, null);
        }
        public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter)
        {
            return FindCore(filter, null);
        }
        public long Count(DynamicFilter filter)
        {
            return CountCore(filter);
        }
        public long Count(Expression<Func<T, bool>> filter)
        {
            return CountCore(filter);
        }

        public bool Exists(Expression<Func<T, bool>> where)
        {
            return ExistsCore(where, null);
        }
        public bool Exists(DynamicFilter where)
        {
            return ExistsCore(where, null);
        }

        public virtual void Add(T entity)
        {
            Add(new List<T>() { entity });
        }
        public virtual void Add(IEnumerable<T> entities)
        {
            using (var scope = NewTransactionScope())
            {
                AddCore(entities, scope.SessionHandle);
                scope.CommitTransaction();
            }
        }
        public virtual ReplaceOneResult Replace(T entity)
        {
            using (var scope = NewTransactionScope())
            {
                var result = ReplaceCore(entity, scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        public virtual object Delete(string id)
        {
            using (var scope = NewTransactionScope())
            {
                var result = DeleteCore(FilterBuilder.Eq(x => Fields.BsonId, id), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }
        public virtual object Delete(IEnumerable<string> ids)
        {
            using (var scope = NewTransactionScope())
            {
                var result = DeleteCore(FilterBuilder.In(x => Fields.BsonId, ids), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }
        public virtual object Delete(DynamicFilter filter)
        {
            using (var scope = NewTransactionScope())
            {
                var result = DeleteCore(filter.ToFilterDefinition<T>(), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        #endregion

        #region Async Methods

        public Task<T?> GetAsync(string id)
        {
            return GetCoreAsync(id, null);
        }
        public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options)
        {
            return FindCoreAsync(options, null);
        }

        public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter)
        {
            return FindCoreAsync(filter, null);
        }
        public Task<long> CountAsync(DynamicFilter filter)
        {
            return CountCoreAsync(filter);
        }
        public Task<long> CountAsync(Expression<Func<T, bool>> filter)
        {
            return CountCoreAsync(filter);
        }
        public Task<bool> ExistsAsync(Expression<Func<T, bool>> where)
        {
            return ExistsCoreAsync(where, null);
        }
        public Task<bool> ExistsAsync(DynamicFilter where)
        {
            return ExistsCoreAsync(where, null);
        }
        public virtual Task AddAsync(T entity)
        {
            return AddAsync(new List<T> { entity });
        }
        public virtual async Task AddAsync(IEnumerable<T> entities)
        {
            using (var scope = NewTransactionScope())
            {
                await AddCoreAsync(entities, scope.SessionHandle);
                scope.CommitTransaction();
                return;
            }
        }

        public virtual async Task<ReplaceOneResult> ReplaceAsync(T entity)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await ReplaceCoreAsync(entity, scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }
        public virtual async Task<object> DeleteAsync(string id)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await DeleteCoreAsync(FilterBuilder.Eq(x => Fields.BsonId, id), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }
        public virtual async Task<object> DeleteAsync(IEnumerable<string> ids)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await DeleteCoreAsync(FilterBuilder.In(x => Fields.BsonId, ids), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }
        public virtual async Task<object> DeleteAsync(DynamicFilter filter)
        {
            using (var scope = NewTransactionScope())
            {
                var result = await DeleteCoreAsync(filter.ToFilterDefinition<T>(), scope.SessionHandle);
                scope.CommitTransaction();
                return result;
            }
        }

        #endregion
    }
}
