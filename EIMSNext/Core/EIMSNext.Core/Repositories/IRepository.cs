using System.Linq.Expressions;
using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Query;
using Microsoft.EntityFrameworkCore.Query;

namespace EIMSNext.Core.Repositories;

/// <summary>
/// 实体仓储抽象。命名保留 Mongo 前缀是历史原因（<see cref="IEntityKey"/> 同理），
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
    /// <param name="id">实体主键 ID。</param>
    /// <returns>匹配的实体，未找到时为 null。</returns>
    T? Get(string id);

    /// <summary>
    /// 异步根据主键 ID 获取实体。
    /// </summary>
    /// <param name="id">实体主键 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的实体，未找到时为 null。</returns>
    Task<T?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态查询选项查询。这是原 <c>Find(MongoFindOptions&lt;T&gt;)</c> 的 EF Core 等价入口，
    /// 过滤走表达式树、排序支持字符串字段名。
    /// </summary>
    /// <param name="options">查询选项。</param>
    /// <returns>惰性查询，可继续链式操作。</returns>
    IQueryable<T> Find(QueryFindOptions<T> options);

    /// <summary>
    /// 按动态表单查询选项查询。Mongo 时期 <c>Find(MongoFindOptions&lt;T&gt;)</c> 的直接等价物：
    /// 内部把动态筛选/排序/分页翻译为 <see cref="QueryFindOptions{T}"/>。
    /// </summary>
    /// <param name="options">动态表单查询选项。</param>
    /// <returns>惰性查询，可继续链式操作。</returns>
    IQueryable<T> Find(DynamicFindOptions<T> options);

    /// <summary>
    /// 按动态表单查询选项异步取列表。
    /// </summary>
    /// <param name="options">动态表单查询选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体列表。</returns>
    Task<List<T>> FindAsync(DynamicFindOptions<T> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按过滤谓词查询。
    /// </summary>
    /// <param name="filter">过滤谓词。</param>
    /// <returns>惰性查询，可继续链式操作。</returns>
    IQueryable<T> Find(Expression<Func<T, bool>> filter);

    /// <summary>
    /// 按动态筛选条件查询。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <returns>惰性查询，可继续链式操作。</returns>
    IQueryable<T> Find(DynamicFilter filter);

    /// <summary>
    /// 按动态查询选项异步取列表。
    /// </summary>
    /// <param name="options">查询选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体列表。</returns>
    Task<List<T>> FindAsync(QueryFindOptions<T> options, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按过滤谓词异步取列表。
    /// </summary>
    /// <param name="filter">过滤谓词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体列表。</returns>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态筛选条件异步取列表。保留此重载是为了让原
    /// <c>Find(new MongoFindOptions&lt;T&gt; { Filter = filter })</c> 调用点最小改动。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体列表。</returns>
    Task<List<T>> FindAsync(DynamicFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态筛选条件取列表（同步）。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <returns>实体列表。</returns>
    List<T> FindList(DynamicFilter filter);

    /// <summary>
    /// 统计满足条件的实体数量。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <returns>实体数量。</returns>
    long Count(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 异步统计满足条件的实体数量。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体数量。</returns>
    Task<long> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计满足动态筛选条件的实体数量。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <returns>实体数量。</returns>
    long Count(DynamicFilter filter);

    /// <summary>
    /// 取指定字段（支持 jsonb 内部路径）的去重值集合。Mongo 时期
    /// <c>DistinctFieldValuesAsync(DynamicFilter, string field, session)</c> 的等价物。
    /// </summary>
    /// <param name="filter">动态筛选条件，可为 null 表示不过滤。</param>
    /// <param name="fieldPath">字段路径。</param>
    /// <param name="limit">最大返回条数，0 表示不限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>去重后的字段值集合。</returns>
    Task<List<object?>> DistinctFieldValuesAsync(
        DynamicFilter? filter,
        string fieldPath,
        int limit = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步统计满足动态筛选条件的实体数量。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实体数量。</returns>
    Task<long> CountAsync(DynamicFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否存在满足条件的实体。命中即返回，比 Count 更省。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存在时为 true。</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否存在满足动态筛选条件的实体。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存在时为 true。</returns>
    Task<bool> AnyAsync(DynamicFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 投影查询。只取需要的列，避免把 jsonb 大字段整行带出来。
    /// </summary>
    /// <typeparam name="TResult">投影结果类型。</typeparam>
    /// <param name="selector">投影表达式。</param>
    /// <returns>投影后的查询。</returns>
    IQueryable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>
    /// 分页读取。PostgreSQL 下先 OrderBy 再 Skip/Take，否则顺序不稳定。
    /// </summary>
    /// <typeparam name="TKey">排序键类型。</typeparam>
    /// <param name="orderBy">排序表达式。</param>
    /// <param name="skip">跳过记录数。</param>
    /// <param name="take">返回记录数。</param>
    /// <returns>分页后的查询。</returns>
    IQueryable<T> Page<TKey>(Expression<Func<T, TKey>> orderBy, int skip, int take);

    #endregion

    #region 写入

    /// <summary>
    /// 新增单个实体。
    /// </summary>
    /// <param name="entity">要新增的实体。</param>
    void Insert(T entity);

    /// <summary>
    /// 批量新增实体。Mongo 时期 <c>Insert(IEnumerable&lt;T&gt;, session)</c> 的等价物。
    /// </summary>
    /// <param name="entities">要新增的实体集合。</param>
    void Insert(IEnumerable<T> entities);

    /// <summary>
    /// 异步新增单个实体。
    /// </summary>
    /// <param name="entity">要新增的实体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task InsertAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步批量新增实体。
    /// </summary>
    /// <param name="entities">要新增的实体集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task InsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 替换（整行更新）单个实体。
    /// </summary>
    /// <param name="entity">要替换的实体。</param>
    void Replace(T entity);

    /// <summary>
    /// 异步替换（整行更新）单个实体。
    /// </summary>
    /// <param name="entity">要替换的实体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ReplaceAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新单个实体的指定字段（同步）。Mongo 时期 <c>Update(string id, UpdateDefinition)</c> 的等价物。
    /// </summary>
    /// <param name="id">实体主键 ID。</param>
    /// <param name="setters">字段更新表达式。</param>
    /// <returns>受影响行数。</returns>
    int Update(string id, Action<UpdateSettersBuilder<T>> setters);

    /// <summary>
    /// 更新单个实体的指定字段。
    /// </summary>
    /// <param name="predicate">目标实体谓词。</param>
    /// <param name="setters">字段更新表达式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> UpdateAsync(
        Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 更新指定字段。
    /// </summary>
    /// <param name="id">实体主键 ID。</param>
    /// <param name="setters">字段更新表达式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> UpdateAsync(
        string id,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新（同步）。翻译为单条 UPDATE，不把实体加载进内存。
    /// Mongo 时期 <c>UpdateMany(FilterDefinition, UpdateDefinition)</c> 的等价入口。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <param name="setters">字段更新表达式。</param>
    /// <returns>受影响行数。</returns>
    int UpdateMany(
        Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setters);

    /// <summary>
    /// 批量更新，翻译为单条 UPDATE，不把实体加载进内存。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <param name="setters">字段更新表达式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> UpdateManyAsync(
        Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态筛选条件批量更新（同步）。Mongo 时期
    /// <c>UpdateMany(DynamicFilter, UpdateDefinition)</c> 的等价入口。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="setters">字段更新表达式。</param>
    /// <returns>受影响行数。</returns>
    int UpdateMany(
        DynamicFilter filter,
        Action<UpdateSettersBuilder<T>> setters);

    /// <summary>
    /// 删除单个实体（物理删除）。
    /// </summary>
    /// <param name="entity">要删除的实体。</param>
    void Delete(T entity);

    /// <summary>
    /// 按主键 ID 物理删除单个实体。Mongo 时期 <c>Delete(string id)</c> 的等价物。
    /// </summary>
    /// <param name="id">实体主键 ID。</param>
    /// <returns>受影响行数。</returns>
    int Delete(string id);

    /// <summary>
    /// 按主键 ID 集合批量物理删除。Mongo 时期 <c>Delete(IEnumerable&lt;string&gt; ids)</c> 的等价物。
    /// </summary>
    /// <param name="ids">实体主键 ID 集合。</param>
    /// <returns>受影响行数。</returns>
    int Delete(IEnumerable<string> ids);

    /// <summary>
    /// 按动态筛选条件批量物理删除。Mongo 时期 <c>Delete(DynamicFilter)</c> 的等价物。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <returns>受影响行数。</returns>
    int Delete(DynamicFilter filter);

    /// <summary>
    /// 异步删除单个实体（物理删除）。
    /// </summary>
    /// <param name="entity">要删除的实体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 物理删除单个实体。Mongo 时期 <c>DeleteAsync(string id)</c> 的等价物。
    /// </summary>
    /// <param name="id">实体主键 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 集合批量物理删除。Mongo 时期 <c>DeleteAsync(IEnumerable&lt;string&gt; ids)</c> 的等价物。
    /// </summary>
    /// <param name="ids">实体主键 ID 集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> DeleteAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态筛选条件批量物理删除。Mongo 时期 <c>DeleteAsync(DynamicFilter)</c> 的等价物。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> DeleteAsync(DynamicFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按过滤谓词批量物理删除。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> DeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态筛选条件批量物理删除。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> DeleteManyAsync(DynamicFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按主键 ID 集合批量软删除（置 DeleteFlag）。
    /// </summary>
    /// <param name="ids">实体主键 ID 集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> SoftDeleteManyAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按过滤谓词批量软删除（置 DeleteFlag）。
    /// </summary>
    /// <param name="predicate">过滤谓词。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> SoftDeleteManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态筛选条件批量软删除（置 DeleteFlag）。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> SoftDeleteManyAsync(DynamicFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按动态筛选条件批量更新。运算符与值由 <paramref name="setters"/> 表达，
    /// 动态条件下的原地更新很少用，保留重载是为了替换旧的
    /// <c>UpdateMany(FilterDefinition, UpdateDefinition)</c> 调用点。
    /// </summary>
    /// <param name="filter">动态筛选条件。</param>
    /// <param name="setters">字段更新表达式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> UpdateManyAsync(
        DynamicFilter filter,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default);

    #endregion

    #region 其它

    /// <summary>
    /// 获取底层数据库上下文。用于建立事务作用域与执行原生 SQL，
    /// 业务代码应优先通过仓储方法操作，不要直接用它做读写。
    /// </summary>
    Microsoft.EntityFrameworkCore.DbContext DbContext { get; }

    /// <summary>
    /// 生成新主键。Id 保持 string 契约（未迁移为整型），因此产出 32 位无连字符 GUID。
    /// </summary>
    /// <returns>新的主键值。</returns>
    string NewId();

    /// <summary>
    /// 确保实体已有主键，没有时生成一个。
    /// </summary>
    /// <param name="entity">实体。</param>
    /// <returns>实体自身，便于链式调用。</returns>
    T EnsureId(T entity);

    /// <summary>
    /// 批量为实体补齐主键。Mongo 时期 <c>EnsureId(IEnumerable&lt;T&gt;)</c> 的等价物。
    /// </summary>
    /// <param name="entities">实体集合。</param>
    /// <returns>原集合，便于链式调用。</returns>
    IEnumerable<T> EnsureId(IEnumerable<T> entities);

    /// <summary>
    /// 建立仓储级事务作用域。对标 Mongo 时期 <c>IRepository.NewTransactionScope()</c>：
    /// 用底层 <see cref="DbContext"/> 开启环境事务，嵌套调用复用最外层事务。
    /// </summary>
    /// <returns>事务作用域，<c>using</c> 结束时未显式提交则回滚。</returns>
    TransactionScope NewTransactionScope();

    /// <summary>
    /// 立即提交挂起的变更。绝大多数场景不需要手工调用——
    /// 仓储的写方法内部已提交，事务作用域提交时也会统一 SaveChanges。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响行数。</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    #endregion
}
