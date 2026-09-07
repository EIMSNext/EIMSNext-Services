using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;

using MongoDB.Driver;

namespace EIMSNext.ApiService
{
    /// <summary>
    /// API 服务基础接口。
    /// </summary>
    public interface IApiService
    { }

    /// <summary>
    /// 泛型 API 服务接口，定义实体 <typeparamref name="T"/> 的基础查询与增删改操作。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
    /// <typeparam name="V">视图模型类型，继承自 <typeparamref name="T"/>。</typeparam>
    public interface IApiService<T, V> : IApiService
        where T : IMongoEntity
        where V : T, new()
    {
        /// <summary>
        /// 根据主键 ID 获取视图模型。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的视图模型，未找到时为 null。</returns>
        V? Get(string id);

        /// <summary>
        /// 获取全部视图模型的可查询对象。
        /// </summary>
        /// <returns>视图模型的可查询对象。</returns>
        IQueryable<V> All();

        /// <summary>
        /// 根据表达式过滤条件查询视图模型。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>视图模型的可查询对象。</returns>
        IQueryable<V> Query(Expression<Func<V, bool>> where);

        /// <summary>
        /// 根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        IFindFluent<T, T> Find(DynamicFindOptions<T> options);

        /// <summary>
        /// 根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>可进一步链式操作的查询流。</returns>
        IFindFluent<T, T> Find(Expression<Func<T, bool>> filter);

        /// <summary>
        /// 统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        long Count(DynamicFilter filter);

        /// <summary>
        /// 统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        long Count(Expression<Func<T, bool>> filter);

        /// <summary>
        /// 判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        bool Exists(Expression<Func<T, bool>> where);

        /// <summary>
        /// 判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        bool Exists(DynamicFilter where);

        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        Task<T?> GetAsync(string id);

        /// <summary>
        /// 异步根据动态查询选项查找实体。
        /// </summary>
        /// <param name="options">动态查询选项。</param>
        /// <returns>异步游标。</returns>
        Task<IAsyncCursor<T>> FindAsync(DynamicFindOptions<T> options);

        /// <summary>
        /// 异步根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>异步游标。</returns>
        Task<IAsyncCursor<T>> FindAsync(Expression<Func<T, bool>> filter);

        /// <summary>
        /// 异步统计满足动态过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>实体数量。</returns>
        Task<long> CountAsync(DynamicFilter filter);

        /// <summary>
        /// 异步统计满足表达式过滤条件的实体数量。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        /// <returns>实体数量。</returns>
        Task<long> CountAsync(Expression<Func<T, bool>> filter);

        /// <summary>
        /// 异步判断是否存在满足表达式过滤条件的实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        Task<bool> ExistsAsync(Expression<Func<T, bool>> where);

        /// <summary>
        /// 异步判断是否存在满足动态过滤条件的实体。
        /// </summary>
        /// <param name="where">动态过滤条件。</param>
        /// <returns>存在时为 true，否则为 false。</returns>
        Task<bool> ExistsAsync(DynamicFilter where);

        /// <summary>
        /// 异步新增单个实体。
        /// </summary>
        /// <param name="entity">要新增的实体。</param>
        Task AddAsync(T entity);

        /// <summary>
        /// 异步替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>替换结果。</returns>
        Task<ReplaceOneResult> ReplaceAsync(T entity);

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>删除结果。</returns>
        Task<object> DeleteAsync(string id);

        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <returns>删除结果。</returns>
        Task<object> DeleteAsync(IEnumerable<string> ids);

        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>删除结果。</returns>
        Task<object> DeleteAsync(DynamicFilter filter);
    }
}
