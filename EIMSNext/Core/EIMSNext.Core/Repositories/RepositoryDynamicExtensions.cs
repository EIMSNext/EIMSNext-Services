using System.Linq.Expressions;

using EIMSNext.Core.Abstractions;
using EIMSNext.Core.Query;
using EIMSNext.Core.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace EIMSNext.Core;

/// <summary>
/// 仓储的动态筛选（<see cref="DynamicFilter"/>）入口。
/// </summary>
/// <remarks>
/// 这些入口的共同点是：先把动态条件翻译成表达式树，再转调同名的表达式版仓储成员。
/// 它们不依赖仓储的具体实现，因此用扩展方法提供而不是接口成员 ——
/// 调用语法与实例方法完全一致，但 <see cref="IRepository{T}"/> 不必为每种动态条件重复声明成员，
/// <see cref="DbRepository{T}"/> 与测试桩也不必各自再实现一遍。
/// <para>
/// 转调表达式版成员（而非直接拼 IQueryable）是刻意的：这样子类对表达式版成员的 override
/// 依然生效，不会出现「扩展方法静态分派绕过子类拦截」的隔离漏洞。
/// </para>
/// </remarks>
public static class RepositoryDynamicExtensions
{
    /// <summary>
    /// 内部把动态筛选/排序/分页翻译为 <see cref="QueryFindOptions{T}"/>。
    /// </summary>
    /// <returns>惰性查询，可继续链式操作。</returns>
    public static IQueryable<T> Find<T>(this IRepository<T> repository, DynamicFindOptions<T> options)
        where T : class, IEntityKey
        => repository.Find(options.ToQueryFindOptions<T>());

    /// <summary>
    /// 按动态表单查询选项异步取列表。
    /// </summary>
    public static Task<List<T>> FindAsync<T>(
        this IRepository<T> repository,
        DynamicFindOptions<T> options,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
        => repository.FindAsync(options.ToQueryFindOptions<T>(), cancellationToken);

    /// <summary>
    /// 按动态筛选条件查询。
    /// </summary>
    /// <returns>惰性查询，可继续链式操作。</returns>
    public static IQueryable<T> Find<T>(this IRepository<T> repository, DynamicFilter filter)
        where T : class, IEntityKey
        => repository.Find(filter.ToPredicate<T>());

    /// <summary>
    /// 按动态筛选条件异步取列表。
    /// </summary>
    /// <remarks>
    /// 走 <see cref="QueryFindOptions{T}"/> 而不是直接 <c>Where</c>：与表达式版一致地套用默认排序与分页上限，
    /// 避免无限定条件时把整表拉进内存。
    /// </remarks>
    public static Task<List<T>> FindAsync<T>(
        this IRepository<T> repository,
        DynamicFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
        => repository.FindAsync(new QueryFindOptions<T>(filter.ToPredicate<T>()), cancellationToken);

    /// <summary>
    /// 按动态筛选条件取列表（同步）。
    /// </summary>
    public static List<T> FindList<T>(this IRepository<T> repository, DynamicFilter filter)
        where T : class, IEntityKey
        => repository.Find(new QueryFindOptions<T>(filter.ToPredicate<T>())).ToList();

    /// <summary>
    /// 统计满足动态筛选条件的实体数量。
    /// </summary>
    public static long Count<T>(this IRepository<T> repository, DynamicFilter filter)
        where T : class, IEntityKey
        => repository.Count(filter.ToPredicate<T>());

    /// <summary>
    /// 异步统计满足动态筛选条件的实体数量。
    /// </summary>
    public static Task<long> CountAsync<T>(
        this IRepository<T> repository,
        DynamicFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
        => repository.CountAsync(filter.ToPredicate<T>(), cancellationToken);

    /// <summary>
    /// 是否存在满足动态筛选条件的实体。
    /// </summary>
    public static Task<bool> AnyAsync<T>(
        this IRepository<T> repository,
        DynamicFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
        => repository.AnyAsync(filter.ToPredicate<T>(), cancellationToken);

    /// <summary>
    /// 按动态筛选条件批量更新，翻译为单条 UPDATE。
    /// </summary>
    public static Task<int> UpdateManyAsync<T>(
        this IRepository<T> repository,
        DynamicFilter filter,
        Action<UpdateSettersBuilder<T>> setters,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
        => repository.UpdateAsync(filter.ToPredicate<T>(), setters, cancellationToken);

    /// <summary>
    /// 按动态筛选条件批量物理删除。
    /// </summary>
    public static Task<int> DeleteAsync<T>(
        this IRepository<T> repository,
        DynamicFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
        => repository.DeleteManyAsync(filter.ToPredicate<T>(), cancellationToken);

    /// <summary>
    /// 按动态筛选条件批量物理删除。
    /// </summary>
    public static Task<int> DeleteManyAsync<T>(
        this IRepository<T> repository,
        DynamicFilter filter,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
        => repository.DeleteManyAsync(filter.ToPredicate<T>(), cancellationToken);

    /// <summary>
    /// 取某动态字段的去重值，供筛选选项下拉使用。
    /// </summary>
    /// <remarks>
    /// PostgreSQL 下 <c>Data</c> 是 jsonb，无法用 <c>SELECT DISTINCT</c> 直接取内部键，
    /// 因此这里在物化后于内存里去重。调用方（筛选选项）本身就带 limit，
    /// 且表单数据量受企业维度约束，这个代价可接受；
    /// 若后续成为瓶颈，应改为对 jsonb 键建表达式索引 + 原生 SQL <c>SELECT DISTINCT</c>。
    /// </remarks>
    public static async Task<List<object?>> DistinctFieldValuesAsync<T>(
        this IRepository<T> repository,
        DynamicFilter? filter,
        string fieldPath,
        int limit = 0,
        CancellationToken cancellationToken = default)
        where T : class, IEntityKey
    {
        if (string.IsNullOrWhiteSpace(fieldPath)) return [];

        var predicate = filter.ToPredicate<T>();
        // 只取该字段所在的 jsonb 列，避免把整行（含大 jsonb）拉回来。
        var rootSegment = DynamicPathAccessor.SplitPath(fieldPath).FirstOrDefault();
        if (rootSegment is null) return [];

        var rootProperty = DynamicPathAccessor.FindProperty(typeof(T), rootSegment);
        if (rootProperty is null) return [];

        if (!DynamicPathAccessor.IsDynamicContainer(rootProperty.PropertyType))
        {
            // 非 jsonb 路径：可用 EF 的 Distinct 走数据库去重。
            var query = repository.Queryable.Where(predicate);
            // 通过反射拿到属性访问表达式后投影，保持强类型。
            var selector = BuildProjection<T>(rootProperty);
            var values = await query.Select(selector).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
            var filtered = values.Where(x => x is not null).ToList();
            return limit > 0 ? filtered.Take(limit).Cast<object?>().ToList() : filtered.Cast<object?>().ToList();
        }

        var rows = await repository.Queryable.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
        var path = DynamicPathAccessor.SplitPath(fieldPath);

        var result = new List<object?>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var value = DynamicPathValueReader.Read(row, path);
            foreach (var flattened in DynamicPathValueReader.Flatten(value))
            {
                if (!seen.Add(DynamicPathValueReader.ToDedupKey(flattened))) continue;
                result.Add(flattened);
                if (limit > 0 && result.Count >= limit) return result;
            }
        }

        return result;
    }

    /// <summary>
    /// 构造从 <typeparamref name="T"/> 到指定属性值的投影表达式。
    /// </summary>
    private static Expression<Func<T, object?>> BuildProjection<T>(System.Reflection.PropertyInfo property)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var access = Expression.Property(parameter, property);
        var boxed = property.PropertyType.IsValueType
            ? Expression.Convert(access, typeof(object))
            : (Expression)access;
        return Expression.Lambda<Func<T, object?>>(boxed, parameter);
    }
}
