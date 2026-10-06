using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Query;
using Microsoft.EntityFrameworkCore.Query;

namespace EIMSNext.Core.Repositories;

/// <summary>
/// 实现已完全基于 EF Core + PostgreSQL。
/// </summary>
/// <typeparam name="T">实现 <see cref="IEntityKey"/> 的实体类型。</typeparam>
public interface IRepository<T> where T : class, IEntityKey
{
    #region 查询

    /// <summary>
    /// 实体集合，默认 AsNoTracking。绝大多数读取只用于投影返回，跟踪实体既浪费内存，
    /// 也会让批处理场景的变更跟踪开销随行数线性增长。
    /// </summary>
    IQueryable<T> Queryable { get; }

    /// <summary>
    /// 根据主键 ID 获取实体。
    /// </summary>
    /// <returns>匹配的实体，未找到时为 null。</returns>
    T? Get(string id);

    /// <summary>
    /// 异步根据主键 ID 获取实体。
    /// </summary>
    /// <returns>匹配的实体，未找到时为 null。</returns>
    Task<T?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 过滤走表达式树、排序支持字符串字段名。
    /// </summary>
    /// <returns>惰性查询，可继续链式操作。</returns>
    IQueryable<T> Find(QueryFindOptions<T> options);

    /// <summary>
    /// 按过滤谓词查询。
    /// </summary>
    /// <returns>惰性查询，可继续链式操作。</returns>
    IQueryable<T> Find(Expression<Func<T, bool>> filter);

    /// <summary>
    /// 按动态查询选项异步取列表。
    /// </summary>
    Task<List<T>> FindAsync(QueryFindOptions<T> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按过滤谓词异步取列表。
    /// </summary>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计满足条件的实体数量。
    /// </summary>
    long Count(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 异步统计满足条件的实体数量。
    /// </summary>
    Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否存在满足条件的实体。命中即返回，比 Count 更省。
    /// </summary>
    /// <returns>存在时为 true。</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region 写入

    /// <summary>
    /// 新增单个实体。
    /// </summary>
    void Insert(T entity);

    /// <summary>
    /// 批量新增实体。
    /// </summary>
    void Insert(IEnumerable<T> entities);

    /// <summary>
    /// 异步新增单个实体。
    /// </summary>
    Task InsertAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步批量新增实体。
    /// </summary>
    Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 替换（整行更新）单个实体。
    /// </summary>
    void Replace(T entity);

    /// <summary>
    /// 异步替换（整行更新）单个实体。
    /// </summary>
    Task ReplaceAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新单个实体的指定字段。
    /// </summary>
    Task<int> UpdateAsync(
        Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 更新指定字段。
    /// </summary>
    Task<int> UpdateAsync(
        string id,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新（同步）。翻译为单条 UPDATE，不把实体加载进内存。
    /// </summary>
    int UpdateMany(
        Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setters);

    /// <summary>
    /// 批量更新，翻译为单条 UPDATE，不把实体加载进内存。
    /// </summary>
    Task<int> UpdateManyAsync(
        Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除单个实体（物理删除）。
    /// </summary>
    void Delete(T entity);

    /// <summary>
    /// 按主键 ID 删除单个实体（物理删除）。
    /// </summary>
    int Delete(string id);

    /// <summary>
    /// 按主键 ID 集合批量物理删除。
    /// </summary>
    int Delete(IEnumerable<string> ids);

    /// <summary>
    /// 异步删除单个实体（物理删除）。
    /// </summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 异步删除单个实体（物理删除）。
    /// </summary>
    Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 集合异步批量物理删除。
    /// </summary>
    Task<int> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按过滤谓词批量物理删除。
    /// </summary>
    Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 集合批量软删除（置 DeleteFlag，并记录删除人/删除时间）。
    /// </summary>
    /// <param name="ids">主键 ID 集合。</param>
    /// <param name="deleteBy">删除人；无操作者上下文时传 null。</param>
    /// <param name="deleteTime">删除时间（Unix 毫秒）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<int> SoftDeleteManyAsync(
        IEnumerable<string> ids,
        Operator? deleteBy,
        long? deleteTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按过滤谓词批量软删除（置 DeleteFlag，并记录删除人/删除时间）。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <param name="deleteBy">删除人；无操作者上下文时传 null。</param>
    /// <param name="deleteTime">删除时间（Unix 毫秒）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<int> SoftDeleteManyAsync(
        Expression<Func<T, bool>> predicate,
        Operator? deleteBy,
        long? deleteTime,
        CancellationToken cancellationToken = default);

    #endregion

    #region 其它

    /// <summary>
    /// 获取底层数据库上下文。用于建立事务作用域与执行原生 SQL，
    /// 业务代码应优先通过仓储方法操作，不要直接用它做读写。
    /// </summary>
    Microsoft.EntityFrameworkCore.DbContext DbContext { get; }

    /// <summary>
    /// 生成新主键。Id 保持 string 契约，因此产出 32 位无连字符 GUID。
    /// </summary>
    string NewId();

    /// <summary>
    /// 确保实体已有主键，没有时生成一个。
    /// </summary>
    T EnsureId(T entity);

    /// <summary>
    /// 确保一批实体都已有主键，没有的按序生成。
    /// </summary>
    /// <returns>原实体序列（主键已就地补齐）。</returns>
    IEnumerable<T> EnsureId(IEnumerable<T> entities);

    /// <summary>
    /// 用底层 <see cref="DbContext"/> 开启环境事务，嵌套调用复用最外层事务。
    /// </summary>
    /// <returns>事务作用域，<c>using</c> 结束时未显式提交则回滚。</returns>
    TransactionScope NewTransactionScope();

    #endregion
}
