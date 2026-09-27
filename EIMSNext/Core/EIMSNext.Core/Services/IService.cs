using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Entities;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 服务接口，定义实体 <typeparamref name="T"/> 的基础查询与增删改操作。
    /// </summary>
    /// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
    /// <remarks>
    /// <list type="bullet">
    /// 需要时用 <see cref="All"/> / <see cref="Query"/> 拿 <see cref="IQueryable{T}"/>。</description></item>
    /// <item><description><c>ReplaceOneResult</c> 与 <c>object</c> 删除结果统一为受影响行数 <see cref="int"/>。</description></item>
    /// </list>
    /// </remarks>
    public interface IService<T> where T : IEntityKey
    {
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
        IQueryable<T> Find(DynamicFindOptions<T> options);

        /// <summary>
        /// 根据表达式过滤条件查找实体。
        /// </summary>
        /// <param name="filter">过滤条件表达式。</param>
        IQueryable<T> Find(Expression<Func<T, bool>> filter);

        /// <summary>
        /// 根据动态过滤条件查找实体。
        /// </summary>
        IQueryable<T> Find(DynamicFilter filter);

        /// <summary>
        /// 根据动态筛选条件查找实体列表。
        /// </summary>
        List<T> FindList(DynamicFilter filter);

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
        /// 替换（整行更新）单个实体。
        /// </summary>
        int Replace(T entity);

        /// <summary>
        /// 根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        int Delete(string id);

        /// <summary>
        /// 根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        int Delete(IEnumerable<string> ids);

        /// <summary>
        /// 根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        int Delete(DynamicFilter filter);

        #endregion

        #region Async Methods

        /// <summary>
        /// 异步根据主键 ID 获取实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        /// <returns>匹配的实体，未找到时为 null。</returns>
        Task<T?> GetAsync(string id);

        /// <summary>
        /// 异步根据动态查询选项查找实体列表。
        /// </summary>
        Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据表达式过滤条件查找实体列表。
        /// </summary>
        Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步根据动态筛选条件查找实体列表。
        /// </summary>
        Task<List<T>> FindAsync(DynamicFilter filter, CancellationToken cancellationToken = default);

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
        /// 异步替换（整行更新）单个实体。
        /// </summary>
        Task<int> ReplaceAsync(T entity);

        /// <summary>
        /// 异步根据主键 ID 删除实体。
        /// </summary>
        /// <param name="id">实体主键 ID。</param>
        Task<int> DeleteAsync(string id);

        /// <summary>
        /// 异步根据多个主键 ID 批量删除实体。
        /// </summary>
        /// <param name="ids">实体主键 ID 集合。</param>
        Task<int> DeleteAsync(IEnumerable<string> ids);

        /// <summary>
        /// 异步根据动态过滤条件批量删除实体。
        /// </summary>
        /// <param name="filter">动态过滤条件。</param>
        Task<int> DeleteAsync(DynamicFilter filter);

        #endregion
    }
}
