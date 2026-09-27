using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Mongo.Entities;
using EIMSNext.Core.Query;
using EIMSNext.Core.Mongo.Query;

using MongoDB.Driver;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 服务接口，定义实体 <typeparamref name="T"/> 的基础查询与增删改操作。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IMongoEntity"/> 的实体类型。</typeparam>
    public interface IService<T> where T : IMongoEntity
    {
        /// <summary>
        /// 获取实体对应的 Mongo 集合。
        /// </summary>
        IMongoCollection<T> Collection { get; }

        #region Methods

        /// <summary>
        /// 根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        T? Get(string id);

        /// <summary>
        /// 获取全部实体的可查询对象。
        /// </summary>
        /// <returns>实体的可查询对象。</returns>
        IQueryable<T> All();

        /// <summary>
        /// 根据表达式过滤条件查询实体。
        /// </summary>
        /// <param name="where">过滤条件表达式。</param>
        /// <returns>实体的可查询对象。</returns>
        IQueryable<T> Query(Expression<Func<T, bool>> where);

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
        /// 新增单个实体。
        /// </summary>
        /// <param name="entity">要新增的实体。</param>
        void Add(T entity);

        /// <summary>
        /// 批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        void Add(IEnumerable<T> entities);

        /// <summary>
        /// 替换单个实体。
        /// </summary>
        /// <param name="entity">要替换的实体。</param>
        /// <returns>替换结果。</returns>
        ReplaceOneResult Replace(T entity);

        /// <summary>
        /// 根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>删除结果。</returns>
        object Delete(string id);

        /// <summary>
        /// 根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        /// <returns>删除结果。</returns>
        object Delete(IEnumerable<string> ids);

        /// <summary>
        /// 根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        /// <returns>删除结果。</returns>
        object Delete(DynamicFilter filter);

        #endregion

        #region Async Methods

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
        /// 异步批量新增实体。
        /// </summary>
        /// <param name="entities">要新增的实体集合。</param>
        Task AddAsync(IEnumerable<T> entities);

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

        #endregion
    }
}
