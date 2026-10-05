using System.Linq.Expressions;
using EIMSNext.ApiService.Extensions;
using EIMSNext.Cache;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Services.Extensions;
using EIMSNext.Core.Services;
using HKH.Mef2.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

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
    /// 泛型 API 服务基类，为 <typeparamref name="T"/> 实体提供基础 API 实现。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
    /// <typeparam name="S">服务类型，实现 <see cref="IService{T}"/>。</typeparam>
    /// <remarks>
    /// <c>IFindFluent&lt;T,T&gt;</c> 换为 <see cref="IQueryable{T}"/>，
    /// <c>ReplaceOneResult</c> 换为受影响行数 <see cref="int"/>。
    /// </remarks>
    public abstract class ApiServiceBase<T,  S> : ApiServiceBase, IApiService<T>
        where T : class, IEntityKey
        where S : class, IService<T>
    {
        /// <summary>
        /// 初始化 <see cref="ApiServiceBase{T,S}"/> 类的新实例。
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
        public T? Get(string id)
        {
            return FilterByPermission().FirstOrDefault(t => t.Id == id);
        }

        /// <summary>
        /// 获取全部视图模型的可查询对象。
        /// </summary>
        /// <returns>视图模型的可查询对象。</returns>
        public IQueryable<T> All()
        {
            return FilterByPermission();
        }

        /// <summary>
        /// 根据表达式过滤条件查询视图模型。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>视图模型的可查询对象。</returns>
        public IQueryable<T> Query(Expression<Func<T, bool>> where)
        {
            return FilterByPermission().Where(where);
        }

        /// <summary>
        /// 根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        public IQueryable<T> Find(DynamicFindOptions<T> options)
        {
            return CoreService.Find(options);
        }

        /// <summary>
        /// 根据动态过滤条件查找实体。
        /// </summary>
        public IQueryable<T> Find(DynamicFilter filter)
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
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<T?> GetAsync(string id)
        {
            return CoreService.All()
                .FilterByCorpId(IdentityContext.CurrentCorpId)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        /// <summary>
        /// 异步根据动态查询选项查找实体列表。
        /// </summary>
        public Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default)
        {
            return CoreService.FindAsync(options, cancellationToken);
        }

        /// <summary>
        /// 异步根据表达式过滤条件查找实体列表。
        /// </summary>
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            return CoreService.FindAsync(filter, cancellationToken);
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
        /// 异步替换（整行更新）单个实体。
        /// </summary>
        public virtual Task<int> ReplaceAsync(T entity)
        {
            return ReplaceAsyncCore(entity);
        }

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        public virtual Task<int> DeleteAsync(string id)
        {
            return DeleteAsyncCore([id]);
        }

        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        public virtual Task<int> DeleteAsync(IEnumerable<string> ids)
        {
            return DeleteAsyncCore(ids);
        }

        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        public virtual Task<int> DeleteAsync(DynamicFilter filter)
        {
            return CoreService.DeleteAsync(filter);
        }

        /// <summary>
        /// 获取按权限过滤后的视图模型查询。
        /// </summary>
        /// <returns>视图模型的可查询对象。</returns>
        protected virtual IQueryable<T> FilterByPermission()
        {
            return CoreService.All().FilterByCorpId(IdentityContext.CurrentCorpId);
        }

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
        protected virtual Task<int> ReplaceAsyncCore(T entity)
        {
            return CoreService.ReplaceAsync(entity);
        }

        /// <summary>
        /// 删除实体的核心实现。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        protected virtual Task<int> DeleteAsyncCore(IEnumerable<string> ids)
        {
            return CoreService.DeleteAsync(ids);
        }
    }
}
