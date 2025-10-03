using System.Linq.Expressions;
using EIMSNext.ApiService.Extension;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core;
using EIMSNext.Core.Entity;
using EIMSNext.Core.Query;
using EIMSNext.Core.Service;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    public abstract class ApiServiceBase<T, V> : IApiService<T, V>
        where T : class, IMongoEntity
        where V : T, new()
    {
        public ApiServiceBase(IResolver resolver)
        {
            Resolver = resolver;
            CoreService = resolver.GetService<T>();
            CacheClient = resolver.GetCacheClient();
            MemoryCache = resolver.GetMemoryCache();
            IdentityContext = resolver.GetIdentityContext();
        }

        protected IResolver Resolver { get; private set; }
        protected IService<T> CoreService { get; private set; }
        protected ICacheClient CacheClient { get; private set; }
        protected IMemoryCache MemoryCache { get; private set; }
        protected IIdentityContext IdentityContext { get; private set; }

        public V? Get(string id)
        {
            return FilterByPermission().FirstOrDefault(t => t.Id == id);
        }

        public IQueryable<V> All()
        {
            return FilterByPermission();
        }

        public IQueryable<V> Query(Expression<Func<V, bool>> where)
        {
            return FilterByPermission().Where(where);
        }

        public IFindFluent<T, T> Find(DynamicFindOptions<T> options)
        {
            return CoreService.Find(options);
        }

        public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter)
        {
            return CoreService.Find(filter);
        }

        public long Count(DynamicFilter filter)
        {
            return CoreService.Count(filter);
        }

        public long Count(Expression<Func<T, bool>> filter)
        {
            return CoreService.Count(filter);
        }

        public bool Exists(Expression<Func<T, bool>> where)
        {
            return CoreService.Exists(where);
        }

        public bool Exists(DynamicFilter where)
        {
            return CoreService.Exists(where);
        }

        public Task<T?> GetAsync(string id)
        {
            return CoreService.GetAsync(id);
        }

        public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options)
        {
            return CoreService.FindAsync(options);
        }

        public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter)
        {
            return CoreService.FindAsync(filter);
        }

        public Task<long> CountAsync(DynamicFilter filter)
        {
            return CoreService.CountAsync(filter);
        }

        public Task<long> CountAsync(Expression<Func<T, bool>> filter)
        {
            return CoreService.CountAsync(filter);
        }

        public Task<bool> ExistsAsync(Expression<Func<T, bool>> where)
        {
            return CoreService.ExistsAsync(where);
        }

        public Task<bool> ExistsAsync(DynamicFilter where)
        {
            return CoreService.ExistsAsync(where);
        }

        public virtual Task AddAsync(T entity)
        {           
            return AddAsyncCore(entity);
        }

        public virtual Task<ReplaceOneResult> ReplaceAsync(T entity)
        {
            return ReplaceAsyncCore(entity);
        }

        public virtual Task<object> DeleteAsync(string id)
        {
            return DeleteAsyncCore([id]);
        }

        public virtual Task<object> DeleteAsync(IEnumerable<string> ids)
        {
            return DeleteAsyncCore(ids);
        }

        public virtual Task<object> DeleteAsync(DynamicFilter filter)
        {
            return CoreService.DeleteAsync(filter);
        }

        protected virtual IQueryable<V> FilterByPermission()
        {
            return CoreService.All().Select(TVConvertor);
        }

        protected virtual Expression<Func<T, V>> TVConvertor => ObjectConvert.CastExp<T, V>();

        protected virtual Task AddAsyncCore(T entity)
        {
            if (entity is ICorpOwned entityBase)
            {
                entityBase.CorpId = IdentityContext.CurrentCorpId;
            }

            return CoreService.AddAsync(entity);
        }
        protected virtual Task<ReplaceOneResult> ReplaceAsyncCore(T entity)
        {
            return CoreService.ReplaceAsync(entity);
        }
        protected virtual Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            return CoreService.DeleteAsync(ids);
        }
    }
}
