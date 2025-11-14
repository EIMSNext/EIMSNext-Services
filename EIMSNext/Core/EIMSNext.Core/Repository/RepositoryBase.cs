using System.Linq.Expressions;
using EIMSNext.Common;
using EIMSNext.Core.Entity;
using EIMSNext.Core.MongoDb;
using EIMSNext.Core.Query;
using EIMSNext.MongoDb;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Core.Repository
{
    public abstract class RepositoryBase<T> : IRepository<T> where T : IMongoEntity
    {
        public RepositoryBase(IMongoDbContex dbContext)
        {
            DbContext = dbContext;
            Collection = dbContext.Database.GetCollection<T>(typeof(T).Name);
        }

        public IMongoDbContex DbContext { get; private set; }
        public IMongoCollection<T> Collection { get; private set; }
        public IQueryable<T> Queryable => Collection.AsQueryable();

        public FilterDefinitionBuilder<T> FilterBuilder => Builders<T>.Filter;
        public SortDefinitionBuilder<T> SortBuilder => Builders<T>.Sort;
        public SearchDefinitionBuilder<T> SearchBuilder => Builders<T>.Search;
        public ProjectionDefinitionBuilder<T> ProjectionBuilder => Builders<T>.Projection;
        public UpdateDefinitionBuilder<T> UpdateBuilder => Builders<T>.Update;

        #region Public

        public MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null)
        {
            return new MongoTransactionScope(DbContext, transOptions);
        }

        public virtual IFindFluent<T, T> Find(DynamicFindOptions<T> options, IClientSessionHandle? session = null)
        {
            return FindCore(options, session);
        }
        public virtual IFindFluent<T, T> Find(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null)
        {
            return FindCore(filter, session);
        }
        public virtual IFindFluent<T, T> Find(MongoFindOptions<T> options, IClientSessionHandle? session = null)
        {
            session = GetSessionHandle(session);
            var result = session == null ? Collection.Find(options.Filter, options.Options) : Collection.Find(session, options.Filter, options.Options);

            if (options.Sort != null)
                result = result.Sort(options.Sort);

            //if (options.Projection != null)
            //    result = result.Project<T>(options.Projection);

            return result.Skip(options.Skip).Limit(options.Take);
        }
        public virtual Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options, IClientSessionHandle? session = null)
        {
            return FindCoreAsync(options, session);
        }
        public virtual async Task<IAsyncCursor<T>> FindAsync(MongoFindOptions<T> options, IClientSessionHandle? session = null)
        {
            session = GetSessionHandle(session);
            return await (session == null ? Collection.FindAsync<T>(options.Filter) : Collection.FindAsync(session, options.Filter));
        }
        public virtual async Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null)
        {
            session = GetSessionHandle(session);
            return await (session == null ? Collection.FindAsync<T>(filter) : Collection.FindAsync(session, filter));
        }

        public virtual long Count(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCore(filter.ToFilterDefinition<T>(), session, options);
        }
        public virtual long Count(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCore(filter, session, options);
        }
        public virtual long Count(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountCore(filter, session, options);
        }
        public virtual Task<long> CountAsync(DynamicFilter filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountAsync(filter.ToFilterDefinition<T>(), session, options);
        }
        public virtual Task<long> CountAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountAsync(filter, session, options);
        }
        public virtual Task<long> CountAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            return CountAsync(filter, session, options);
        }

        public virtual T? Get(string id, IClientSessionHandle? session = null)
        {
            var idFilter = FilterBuilder.Eq(Fields.BsonId, id);
            session = GetSessionHandle(session);
            var result = session == null ? Collection.Find(idFilter) : Collection.Find(session, idFilter);
            return result.FirstOrDefault();
        }
        public virtual async Task<T?> GetAsync(string id, IClientSessionHandle? session = null)
        {
            var idFilter = FilterBuilder.Eq(Fields.BsonId, id);
            session = GetSessionHandle(session);
            var result = await (session == null ? Collection.FindAsync(idFilter) : Collection.FindAsync(session, idFilter));
            return result.FirstOrDefault();
        }

        public virtual void Insert(T entity, IClientSessionHandle? session = null)
        {
            InsertCore(EnsureId(entity), session);
        }
        public virtual void Insert(IEnumerable<T> entities, IClientSessionHandle? session = null)
        {
            InsertCore(EnsureId(entities), session);
        }
        public virtual Task InsertAsync(T entity, IClientSessionHandle? session = null)
        {
            return InsertCoreAsync(EnsureId(entity), session);
        }
        public virtual Task InsertAsync(IEnumerable<T> entities, IClientSessionHandle? session = null)
        {
            return InsertCoreAsync(EnsureId(entities), session);
        }

        public virtual UpdateResult Update(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCore(FilterBuilder.Eq(Fields.BsonId, id), update, false, upsert, session);
        }
        public virtual Task<UpdateResult> UpdateAsync(string id, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCoreAsync(FilterBuilder.Eq(Fields.BsonId, id), update, false, upsert, session);
        }
        public virtual UpdateResult UpdateMany(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCore(filter.ToFilterDefinition<T>(), update, true, upsert, session); ;
        }
        public virtual Task<UpdateResult> UpdateManyAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCoreAsync(filter.ToFilterDefinition<T>(), update, true, upsert, session); ;
        }
        public virtual UpdateResult UpdateMany(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCore(filter, update, true, upsert, session);
        }
        public virtual Task<UpdateResult> UpdateManyAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert = true, IClientSessionHandle? session = null)
        {
            return UpdateCoreAsync(filter, update, true, upsert, session);
        }

        public ReplaceOneResult Replace(T entity, IClientSessionHandle? session = null)
        {
            return ReplaceCore(entity, session);
        }
        public virtual Task<ReplaceOneResult> ReplaceAsync(T entity, IClientSessionHandle? session = null)
        {
            return ReplaceCoreAsync(entity, session);
        }

        public virtual DeleteResult Delete(string id, IClientSessionHandle? session = null)
        {
            return DeleteCore(FilterBuilder.Eq(Fields.BsonId, id), session);
        }
        public virtual DeleteResult Delete(IEnumerable<string> ids, IClientSessionHandle? session = null)
        {
            return DeleteCore(FilterBuilder.In(Fields.BsonId, ids), session);
        }
        public virtual DeleteResult Delete(DynamicFilter filter, IClientSessionHandle? session = null)
        {
            return DeleteCore(filter.ToFilterDefinition<T>(), session);
        }
        public virtual DeleteResult Delete(FilterDefinition<T> filter, IClientSessionHandle? session = null)
        {
            return DeleteCore(filter, session);
        }

        public virtual Task<DeleteResult> DeleteAsync(string id, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(FilterBuilder.Eq(Fields.BsonId, id), session);
        }
        public virtual Task<DeleteResult> DeleteAsync(IEnumerable<string> ids, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(FilterBuilder.In(Fields.BsonId, ids), session);
        }
        public virtual Task<DeleteResult> DeleteAsync(DynamicFilter filter, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(filter.ToFilterDefinition<T>(), session);
        }
        public virtual Task<DeleteResult> DeleteAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null)
        {
            return DeleteCoreAsync(filter, session);
        }

        #endregion

        #region Core  

        protected virtual IFindFluent<T, T> FindCore(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            var filter = options.Filter == null ? FilterBuilder.Empty : options.Filter.ToFilterDefinition<T>();
            session = GetSessionHandle(session);
            var result = session == null ? Collection.Find(filter) : Collection.Find(session, filter);

            var sort = options.Sort?.ToSortDefinition<T>();
            if (sort != null)
                result = result.Sort(sort);

            //var proj = options.Fields?.ToProjectionDefinition<T>();
            //if (proj != null)
            //    result = result.Project<T>(proj);

            return result.Skip(options.Skip).Limit(options.Take); ;
        }
        protected virtual IFindFluent<T, T> FindCore(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            return session == null ? Collection.Find(filter) : Collection.Find(session, filter);
        }
        protected virtual async Task<IAsyncCursor<T>> FindCoreAsync(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            var filter = options.Filter == null ? FilterBuilder.Empty : options.Filter.ToFilterDefinition<T>();
            session = GetSessionHandle(session);
            return await (session == null ? Collection.FindAsync(filter) : Collection.FindAsync(session, filter));
        }
        protected virtual async Task<IAsyncCursor<T>> FindCoreAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            return await (session == null ? Collection.FindAsync(filter) : Collection.FindAsync(session, filter));
        }

        public virtual long CountCore(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocuments(filter, options) : Collection.CountDocuments(session, filter, options));
        }
        public virtual long CountCore(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocuments(filter, options) : Collection.CountDocuments(session, filter, options));
        }
        public virtual Task<long> CountCoreAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocumentsAsync(filter, options) : Collection.CountDocumentsAsync(session, filter, options));
        }
        public virtual Task<long> CountCoreAsync(FilterDefinition<T> filter, IClientSessionHandle? session = null, CountOptions? options = null)
        {
            session = GetSessionHandle(session);
            return (session == null ? Collection.CountDocumentsAsync(filter, options) : Collection.CountDocumentsAsync(session, filter, options));
        }

        protected virtual void InsertCore(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                Collection.InsertOne(entity);
            else
                Collection.InsertOne(session, entity);
        }
        protected virtual void InsertCore(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                Collection.InsertMany(entities);
            else
                Collection.InsertMany(session, entities);
        }
        protected virtual Task InsertCoreAsync(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.InsertOneAsync(entity);
            else
                return Collection.InsertOneAsync(session, entity);
        }
        protected virtual Task InsertCoreAsync(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.InsertManyAsync(entities);
            else
                return Collection.InsertManyAsync(session, entities);
        }

        protected virtual UpdateResult UpdateCore(FilterDefinition<T> filter, UpdateDefinition<T> update, bool many, bool upsert, IClientSessionHandle? session)
        {
            var options = new UpdateOptions { IsUpsert = upsert, BypassDocumentValidation = true };
            session = GetSessionHandle(session);
            if (session == null)
                return many ? Collection.UpdateMany(filter, update, options) : Collection.UpdateOne(filter, update, options);
            else
                return many ? Collection.UpdateMany(session, filter, update, options) : Collection.UpdateOne(session, filter, update, options);
        }
        protected virtual Task<UpdateResult> UpdateCoreAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool many, bool upsert, IClientSessionHandle? session)
        {
            var options = new UpdateOptions { IsUpsert = true, BypassDocumentValidation = true };
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.UpdateManyAsync(filter, update, options);
            else
                return Collection.UpdateManyAsync(session, filter, update, options);
        }

        protected virtual ReplaceOneResult ReplaceCore(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.ReplaceOne(GetIdFilter(entity), entity);
            else
                return Collection.ReplaceOne(session, GetIdFilter(entity), entity);
        }
        protected virtual Task<ReplaceOneResult> ReplaceCoreAsync(T entity, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.ReplaceOneAsync(GetIdFilter(entity), entity);
            else
                return Collection.ReplaceOneAsync(session, GetIdFilter(entity), entity);
        }

        protected virtual DeleteResult DeleteCore(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.DeleteMany(filter);
            else
                return Collection.DeleteMany(session, filter);
        }
        protected virtual Task<DeleteResult> DeleteCoreAsync(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            session = GetSessionHandle(session);
            if (session == null)
                return Collection.DeleteManyAsync(filter);
            else
                return Collection.DeleteManyAsync(session, filter);
        }

        #endregion

        #region Helper

        public IEnumerable<T> EnsureId(IEnumerable<T> entities)
        {
            entities.ForEach(x => EnsureId(x));
            return entities;
        }

        public T EnsureId(T entity)
        {
            if (string.IsNullOrEmpty(entity.Id))
            {
                entity.Id = NewId();
            }
            return entity;
        }
        public string NewId()
        {
            return ObjectId.GenerateNewId().ToString();
        }

        protected FilterDefinition<T> GetIdFilter(T entity)
        {
            return FilterBuilder.Eq(Fields.BsonId, entity.Id);
        }

        private IClientSessionHandle? GetSessionHandle(IClientSessionHandle? session)
        {
            if (session != null) return session;
            if (MongoTransactionScope.IsInTransaction) return MongoTransactionScope.Transaction;
            return null;
        }

        #endregion
    }
}
