using System.Linq.Expressions;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Entity;
using EIMSNext.Core.MongoDb;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repository;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Search;

namespace EIMSNext.Core.Service
{
    public abstract class ServiceCore<T> where T : class, IMongoEntity
    {
        #region Variables

        protected static readonly Type IEntityType = typeof(IEntity);
        protected static readonly Type IDeleteFlagType = typeof(IDeleteFlag);
        protected static readonly Type ICorpOwnedType = typeof(ICorpOwned);

        #endregion 

        protected ServiceCore(IResolver resolver)
        {
            Resolver = resolver;
            Repository = resolver.GetRepository<T>();
            CacheClient = resolver.GetCacheClient();
            Logger = resolver.GetLogger<T>();
            Context = resolver.GetServiceContext();
            SessionStore = resolver.Resolve<ISessionStore>();
        }

        #region Properties

        protected IResolver Resolver { get; private set; }
        protected IRepository<T> Repository { get; private set; }
        protected ICacheClient CacheClient { get; private set; }
        protected ILogger<T> Logger { get; private set; }
        protected IServiceContext Context { get; private set; }
        protected ISessionStore SessionStore { get; private set; }
        protected virtual bool LogicDelete => true;

        protected FilterDefinitionBuilder<T> FilterBuilder => Repository.FilterBuilder;
        protected SortDefinitionBuilder<T> SortBuilder => Repository.SortBuilder;
        protected SearchDefinitionBuilder<T> SearchBuilder => Repository.SearchBuilder;
        protected ProjectionDefinitionBuilder<T> ProjectionBuilder => Repository.ProjectionBuilder;
        protected UpdateDefinitionBuilder<T> UpdateBuilder => Repository.UpdateBuilder;

        #endregion

        #region Helper

        protected MongoTransactionScope NewTransactionScope(TransactionOptions? transOptions = null)
        {
            return Repository.NewTransactionScope(transOptions);
        }

        protected virtual void CreateAuditLog(DbAction action, T oldEntity, T newEntity)
        {
        }

        protected virtual S? GetFromStore<S>(string key, DataVersion version = DataVersion.V0) where S : class, IMongoEntity
        {
            return SessionStore.Get<S>(key, version, id => Resolver.GetRepository<S>().Get(id));
        }

        #region Methods

        protected virtual T? GetCore(string id, IClientSessionHandle? session)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Repository.Get(id, session);
        }
        protected virtual IFindFluent<T, T> FindCore(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            return Repository.Find(options, session);
        }
        protected virtual IFindFluent<T, T> FindCore(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            return Repository.Find(filter, session);
        }

        protected virtual long CountCore(DynamicFilter filter)
        {
            return Repository.Count(filter);
        }
        protected virtual long CountCore(Expression<Func<T, bool>> filter)
        {
            return Repository.Count(filter);
        }

        protected virtual bool ExistsCore(Expression<Func<T, bool>> where, IClientSessionHandle? session)
        {
            return Repository.Find(where, session).CountDocuments() > 0;
        }
        protected virtual bool ExistsCore(DynamicFilter where, IClientSessionHandle? session)
        {
            return Repository.Find(new DynamicFindOptions<T> { Filter = where }, session).CountDocuments() > 0;
        }

        //protected virtual void AddCore(T entity, IClientSessionHandle? session)
        //{
        //    FillSystemField(entity, false);
        //    Repository.Insert(entity, session);
        //}
        protected virtual void AddCore(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            entities.ForEach(entity => FillSystemField(entity, false));
            BeforeAdd(entities, session).Wait();
            Repository.Insert(entities, session);
            AfterAdd(entities, session).Wait();
        }
        protected virtual ReplaceOneResult ReplaceCore(T entity, IClientSessionHandle? session)
        {
            FillSystemField(entity, true);
            BeforeReplace(entity, session).Wait();
            var result = Repository.Replace(entity, session);
            AfterReplace(entity, session).Wait();
            return result;
        }
        //protected virtual UpdateResult PatchCore(string id, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.Update(id, update, upsert, session);
        //}
        //protected virtual UpdateResult PatchManyCore(DynamicFilter filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.UpdateMany(filter, update, upsert, session);
        //}
        protected virtual UpdateResult PatchManyCore(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        {
            update = FillSystemField(update);
            BeforeUpdate(filter, update, upsert, session).Wait();
            var result = Repository.UpdateMany(filter, update, upsert, session);
            AfterUpdate(filter, update, upsert, session).Wait();
            return result;
        }
        //protected virtual DeleteResult DeleteCore(string id, IClientSessionHandle? session)
        //{
        //    return Repository.Delete(id, session);
        //}
        //protected virtual DeleteResult DeleteCore(IEnumerable<string> ids, IClientSessionHandle? session)
        //{
        //    return Repository.Delete(ids, session);
        //}
        //protected virtual DeleteResult DeleteCore(DynamicFilter filter, IClientSessionHandle? session)
        //{
        //    return Repository.Delete(filter, session);
        //}

        protected virtual object DeleteCore(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            object result = new object();
            BeforeDelete(filter, session).Wait();
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                var update = UpdateBuilder.Set(Fields.DeleteFlag, true);
                result = Repository.UpdateMany(filter, update, session: session);
            }
            else
                result = Repository.Delete(filter, session);

            AfterDelete(filter, session).Wait();
            return result;
        }

        #endregion

        #region Async Methods

        protected virtual Task<T?> GetCoreAsync(string id, IClientSessionHandle? session)
        {
            return Repository.GetAsync(id, session);
        }
        protected virtual Task<IAsyncCursor<T>> FindCoreAsync(DynamicFindOptions<T> options, IClientSessionHandle? session)
        {
            return Repository.FindAsync(options, session);
        }
        protected virtual Task<IAsyncCursor<T>> FindCoreAsync(Expression<Func<T, bool>> filter, IClientSessionHandle? session)
        {
            return Repository.FindAsync(filter, session);
        }
        protected virtual Task<long> CountCoreAsync(DynamicFilter filter)
        {
            return Repository.CountAsync(filter);
        }
        protected virtual Task<long> CountCoreAsync(Expression<Func<T, bool>> filter)
        {
            return Repository.CountAsync(filter);
        }
        protected virtual async Task<bool> ExistsCoreAsync(Expression<Func<T, bool>> where, IClientSessionHandle? session)
        {
            var cursor = await Repository.FindAsync(where, session);
            return cursor.FirstOrDefault() != null;
        }
        protected virtual async Task<bool> ExistsCoreAsync(DynamicFilter where, IClientSessionHandle? session)
        {
            var cursor = await Repository.FindAsync(new DynamicFindOptions<T> { Filter = where }, session);
            return cursor.FirstOrDefault() != null;
        }
        //protected virtual Task AddCoreAsync(T entity, IClientSessionHandle? session)
        //{
        //    FillSystemField(entity, false);
        //    return Repository.InsertAsync(entity, session);
        //}
        protected virtual async Task AddCoreAsync(IEnumerable<T> entities, IClientSessionHandle? session)
        {
            entities.ForEach(entity => FillSystemField(entity, false));
            await BeforeAdd(entities, session);
            await Repository.InsertAsync(entities, session);

            await AfterAdd(entities, session);
            return;
        }
        //protected virtual Task<UpdateResult> PatchCoreAsync(string id, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.UpdateAsync(id, update, upsert, session);
        //}
        //protected virtual Task<UpdateResult> PatchManyCoreAsync(DynamicFilter filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        //{
        //    update = FillSystemField(update);
        //    return Repository.UpdateManyAsync(filter, update, upsert, session);
        //}
        protected virtual async Task<UpdateResult> PatchManyCoreAsync(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session)
        {
            update = FillSystemField(update);
            await BeforeUpdate(filter, update, upsert, session);
            var result = await Repository.UpdateManyAsync(filter, update, upsert, session);

            await AfterUpdate(filter, update, upsert, session);
            return result;
        }
        protected virtual async Task<ReplaceOneResult> ReplaceCoreAsync(T entity, IClientSessionHandle? session)
        {
            FillSystemField(entity, true);
            await BeforeReplace(entity, session);
            var result = await Repository.ReplaceAsync(entity, session);

            await AfterReplace(entity, session);
            return result;
        }
        //protected virtual Task<DeleteResult> DeleteCoreAsync(string id, IClientSessionHandle? session)
        //{
        //    return Repository.DeleteAsync(id, session);
        //}
        //protected virtual Task<DeleteResult> DeleteCoreAsync(IEnumerable<string> ids, IClientSessionHandle? session)
        //{
        //    return Repository.DeleteAsync(ids, session);
        //}
        //protected virtual Task<DeleteResult> DeleteCoreAsync(DynamicFilter filter, IClientSessionHandle? session)
        //{
        //    return Repository.DeleteAsync(filter, session);
        //}
        protected virtual async Task<object> DeleteCoreAsync(FilterDefinition<T> filter, IClientSessionHandle? session)
        {
            await BeforeDelete(filter, session);
            if (LogicDelete && IDeleteFlagType.IsAssignableFrom(typeof(T)))
            {
                var update = UpdateBuilder.Set(Fields.DeleteFlag, true);
                var result = await Repository.UpdateManyAsync(filter, update, session: session);

                await AfterDelete(filter, session);
                return result;
            }
            else
            {
                var result = await Repository.DeleteAsync(filter, session);

                await AfterDelete(filter, session);
                return result;
            }
        }

        #endregion

        protected virtual UpdateDefinition<T> GetUpdateDefinition(BsonDocument bson)
        {
            var updateList = new List<UpdateDefinition<T>>();
            updateList.AddRange(BuildUpdateDefinition(bson, null));
            return UpdateBuilder.Combine(updateList);
        }
        protected List<UpdateDefinition<T>> BuildUpdateDefinition(BsonDocument bson, string? parent)
        {
            var updateList = new List<UpdateDefinition<T>>();
            foreach (var el in bson!.Elements)
            {
                var key = string.IsNullOrEmpty(parent) ? el.Name : $"{parent}.{el.Name}";
                var subUpdateList = new List<UpdateDefinition<T>>();

                if (el.Value.IsBsonDocument)
                {
                    updateList.AddRange(BuildUpdateDefinition(el.Value.ToBsonDocument(), key));
                }
                else if (el.Value.IsBsonArray)
                {
                    var bsonArray = el.Value.AsBsonArray;
                    var i = 0;
                    foreach (var doc in bsonArray)
                    {
                        if (doc.IsBsonDocument)
                        {
                            updateList.AddRange(BuildUpdateDefinition(doc.ToBsonDocument(), $"{key}.{i}"));
                        }
                        else
                        {
                            updateList.Add(UpdateBuilder.Set(key, el.Value));
                            continue;
                        }

                        i++;
                    }
                }
                else
                {
                    updateList.Add(UpdateBuilder.Set(key, el.Value));
                }
            }

            return updateList;
        }

        protected virtual T FillSystemField(T entity, bool isEdit)
        {
            return entity;
        }
        protected virtual UpdateDefinition<T> FillSystemField(UpdateDefinition<T> update)
        {
            return update;
        }

        #endregion

        #region Business Core

        protected virtual Task BeforeAdd(IEnumerable<T> entities, IClientSessionHandle? session) { return Task.CompletedTask; }
        protected virtual Task AfterAdd(IEnumerable<T> entities, IClientSessionHandle? session) { return Task.CompletedTask; }
        protected virtual Task BeforeReplace(T entity, IClientSessionHandle? session) { return Task.CompletedTask; }
        protected virtual Task AfterReplace(T entity, IClientSessionHandle? session) { return Task.CompletedTask; }
        protected virtual Task BeforeUpdate(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session) { return Task.CompletedTask; }
        protected virtual Task AfterUpdate(FilterDefinition<T> filter, UpdateDefinition<T> update, bool upsert, IClientSessionHandle? session) { return Task.CompletedTask; }
        protected virtual Task BeforeDelete(FilterDefinition<T> filter, IClientSessionHandle? session) { return Task.CompletedTask; }
        protected virtual Task AfterDelete(FilterDefinition<T> filter, IClientSessionHandle? session) { return Task.CompletedTask; }

        #endregion
    }
}
