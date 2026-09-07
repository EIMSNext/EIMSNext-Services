using System.Linq.Expressions;
using EIMSNext.ApiService.Extensions;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Mongo.Repositories;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using HKH.Mef2.Integration;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// API 服务基类，提供依赖解析与通用上下文属性。
    /// </summary>
    public abstract class ApiServiceBase : IApiService
    {
        /// <summary>
        /// 初始化 <see cref="ApiServiceBase"/> 类的新实例。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        public ApiServiceBase(IResolver resolver)
        {
            Resolver = resolver;
            CacheClient = resolver.GetCacheClient();
            MemoryCache = resolver.GetMemoryCache();
            IdentityContext = resolver.GetIdentityContext();
            ServiceContext = resolver.GetServiceContext();
        }

        /// <summary>
        /// 获取依赖解析器。
        /// </summary>
        protected IResolver Resolver { get; private set; }

        /// <summary>
        /// 获取缓存客户端。
        /// </summary>
        protected ICacheClient CacheClient { get; private set; }

        /// <summary>
        /// 获取内存缓存。
        /// </summary>
        protected IMemoryCache MemoryCache { get; private set; }

        /// <summary>
        /// 获取身份上下文。
        /// </summary>
        protected IIdentityContext IdentityContext { get; private set; }

        /// <summary>
        /// 获取服务上下文。
        /// </summary>
        protected IServiceContext ServiceContext { get; private set; }
    }

    /// <summary>
    /// 泛型 API 服务基类，为 <typeparamref name="T"/> 实体提供 <see cref="IApiService{T, V}"/> 的默认实现。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
    /// <typeparam name="V">视图模型类型，继承自 <typeparamref name="T"/>。</typeparam>
    /// <typeparam name="S">服务类型，实现 <see cref="IService{T}"/>。</typeparam>
    public abstract class ApiServiceBase<T, V, S> : ApiServiceBase, IApiService<T, V>
        where T : class, IMongoEntity
        where V : T, new()
        where S : class, IService<T>
    {
        /// <summary>
        /// 初始化 <see cref="ApiServiceBase{T, V, S}"/> 类的新实例。
        /// </summary>
        /// <param name="resolver">依赖解析器。</param>
        public ApiServiceBase(IResolver resolver) : base(resolver)
        {
            CoreService = resolver.GetService<S, T>();
        }

        /// <summary>
        /// 获取核心服务。
        /// </summary>
        protected S CoreService { get; private set; }

        /// <summary>
        /// 根据主键 ID 获取视图模型。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的视图模型，未找到时为 null。</returns>
        public V? Get(string id)
        {
            return FilterByPermission().FirstOrDefault(t => t.Id == id);
        }

        /// <summary>
        /// 获取全部视图模型的可查询对象。
        /// </summary>
        /// <returns>视图模型的可查询对象。</returns>
        public IQueryable<V> All()
        {
            return FilterByPermission();
        }

        /// <summary>
        /// 根据表达式过滤条件查询视图模型。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>视图模型的可查询对象。</returns>
        public IQueryable<V> Query(Expression<Func<V, bool>> where)
        {
            return FilterByPermission().Where(where);
        }

        /// <summary>
        /// 根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        public IFindFluent<T, T> Find(DynamicFindOptions<T> options)
        {
            return CoreService.Find(options);
        }

        /// <summary>
        /// 根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        public IFindFluent<T, T> Find(Expression<Func<T, bool>> filter)
        {
            return CoreService.Find(filter);
        }

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        public long Count(DynamicFilter filter)
        {
            return CoreService.Count(filter);
        }

        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        public long Count(Expression<Func<T, bool>> filter)
        {
            return CoreService.Count(filter);
        }

        /// <summary>
        /// 判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public bool Exists(Expression<Func<T, bool>> where)
        {
            return CoreService.Exists(where);
        }

        /// <summary>
        /// 判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public bool Exists(DynamicFilter where)
        {
            return CoreService.Exists(where);
        }

        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        public Task<T?> GetAsync(string id)
        {
            return CoreService.GetAsync(id);
        }

        /// <summary>
        /// 异步根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <returns>异步游标。</returns>
        public Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options)
        {
            return CoreService.FindAsync(options);
        }

        /// <summary>
        /// 异步根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>异步游标。</returns>
        public Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter)
        {
            return CoreService.FindAsync(filter);
        }

        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        public Task<long> CountAsync(DynamicFilter filter)
        {
            return CoreService.CountAsync(filter);
        }

        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        public Task<long> CountAsync(Expression<Func<T, bool>> filter)
        {
            return CoreService.CountAsync(filter);
        }

        /// <summary>
        /// 异步判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public Task<bool> ExistsAsync(Expression<Func<T, bool>> where)
        {
            return CoreService.ExistsAsync(where);
        }

        /// <summary>
        /// 异步判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        public Task<bool> ExistsAsync(DynamicFilter where)
        {
            return CoreService.ExistsAsync(where);
        }

        /// <summary>
        /// 异步新增单个实体。
        /// </summary>
        /// <param name="entity">要新增的实体。</param>
        public virtual Task AddAsync(T entity)
        {
            return AddAsyncCore(entity);
        }

        /// <summary>
        /// 异步替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>替换结果。</returns>
        public virtual Task<ReplaceOneResult> ReplaceAsync(T entity)
        {
            return ReplaceAsyncCore(entity);
        }

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>删除结果。</returns>
        public virtual Task<object> DeleteAsync(string id)
        {
            return DeleteAsyncCore([id]);
        }

        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <returns>删除结果。</returns>
        public virtual Task<object> DeleteAsync(IEnumerable<string> ids)
        {
            return DeleteAsyncCore(ids);
        }

        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>删除结果。</returns>
        public virtual Task<object> DeleteAsync(DynamicFilter filter)
        {
            return CoreService.DeleteAsync(filter);
        }

        /// <summary>
        /// 获取按权限过滤后的视图模型查询。
        /// </summary>
        /// <returns>视图模型的可查询对象。</returns>
        protected virtual IQueryable<V> FilterByPermission()
        {
            return CoreService.All().FilterByCorpId(IdentityContext.CurrentCorpId).Select(TVConvertor);
        }

        /// <summary>
        /// 获取实体到视图模型的转换表达式。
        /// </summary>
        protected virtual Expression<Func<T, V>> TVConvertor => ObjectConvert.CastExp<T, V>();

        /// <summary>
        /// 新增实体的核心实现。
        /// </summary>
        /// <param name="entity">要新增的实体。</param>
        protected virtual Task AddAsyncCore(T entity)
        {
            return CoreService.AddAsync(entity);
        }

        /// <summary>
        /// 替换实体的核心实现。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>替换结果。</returns>
        protected virtual Task<ReplaceOneResult> ReplaceAsyncCore(T entity)
        {
            return CoreService.ReplaceAsync(entity);
        }

        /// <summary>
        /// 删除实体的核心实现。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <returns>删除结果。</returns>
        protected virtual Task<object> DeleteAsyncCore(IEnumerable<string> ids)
        {
            return CoreService.DeleteAsync(ids);
        }
    }
}
