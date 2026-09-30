namespace EIMSNext.Core.Query;

/// <summary>
/// PostgreSQL 侧自定义 SQL 函数的 C# 入口，仅供 EF Core 查询翻译使用。
/// </summary>
/// <remarks>
/// <para>
/// 这些成员<b>不能在内存中直接调用</b>——它们只是给 EF Core 一个
/// 「C# 方法签名 → 数据库函数」的映射锚点，函数体在
/// <c>ApiHost/EIMSNext.Tool.DbMaintenance/Sql/007_CreateJsonPathFunctions.sql</c> 里创建，
/// 映射在 <c>EIMSNext.Persistence.PostgreSql.EIMSNextModelConfiguration</c> 里用
/// <c>HasDbFunction</c> 注册。在 LINQ 表达式树里调用它们，EF Core 会翻译成对应的 SQL 调用。
/// </para>
/// <para>
/// 放在 <c>EIMSNext.Core</c> 而非 <c>EIMSNext.Persistence.PostgreSql</c> 的原因：
/// 表达式由 <see cref="DynamicPathAccessor"/> 构造，而依赖方向是 Persistence → Core，
/// 反向引用会成环。
/// </para>
/// </remarks>
public static class PgJsonFunctions
{
    /// <summary>
    /// 取 jsonb 路径上第一个值的文本形式。
    /// </summary>
    /// <param name="json">jsonb 列，或已取出的 jsonb 值。</param>
    /// <param name="jsonPath">JSONPath，例如 <c>$."f_1"."label"</c>。</param>
    /// <returns>文本值；路径不可达或值为 JSON null 时为 <c>null</c>。</returns>
    /// <remarks>映射到 <c>"eims_json_text"(jsonb, text)</c>。用于排序、去重等需要取值的场景。</remarks>
    public static string? JsonPath(object json, string jsonPath)
        => throw new NotSupportedException(
            "PgJsonFunctions.JsonPath 只用于 EF Core 查询翻译，不能在内存中直接调用。");

    /// <summary>
    /// 判断 jsonb 路径上是否存在满足条件的元素。
    /// </summary>
    /// <param name="json">jsonb 列，或已取出的 jsonb 值。</param>
    /// <param name="jsonPath">
    /// 带谓词的 JSONPath，例如 <c>$."a"."b" ? (@ &gt; 1)</c>。
    /// 不带谓词时即「路径存在」。
    /// </param>
    /// <returns>存在满足条件的元素时为 <c>true</c>。</returns>
    /// <remarks>
    /// 映射到 <c>"eims_json_match"(jsonb, text)</c>，底层是 <c>jsonb_path_exists</c>。
    /// </remarks>
    public static bool JsonMatch(object json, string jsonPath)
        => throw new NotSupportedException(
            "PgJsonFunctions.JsonMatch 只用于 EF Core 查询翻译，不能在内存中直接调用。");

    /// <summary>
    /// 取 jsonb 路径上第一个值，保留 jsonb 类型，供排序使用。
    /// </summary>
    /// <param name="json">jsonb 列，或已取出的 jsonb 值。</param>
    /// <param name="jsonPath">JSONPath，例如 <c>$."f_1"</c>。</param>
    /// <returns>路径上的第一个值；路径不可达时为 <c>null</c>。</returns>
    /// <remarks>
    /// <para>
    /// 映射到 <c>"eims_json_sort"(jsonb, text)</c>，返回类型在数据库侧是 <c>jsonb</c>。
    /// 声明为 <see cref="string"/> 只是给表达式树一个 CLR 载体——函数调用出现在
    /// <c>ORDER BY</c> 里，不会被投影回客户端，因此不涉及 jsonb → string 的反序列化。
    /// </para>
    /// </remarks>
    public static string? JsonSort(object json, string jsonPath)
        => throw new NotSupportedException(
            "PgJsonFunctions.JsonSort 只用于 EF Core 查询翻译，不能在内存中直接调用。");

    /// <summary>
    /// 判断 jsonb 数组列是否包含指定元素，下推为 <c>jsonb @&gt; jsonb_build_array(element)</c>。
    /// </summary>
    /// <remarks>
    /// 映射到中缀运算符而非函数调用，以命中 jsonb_path_ops GIN 索引
    /// （jsonb_path_ops 只服务 <c>@&gt;</c>，不服务 <c>?</c>/<c>@?</c>/<c>@@</c>）。
    /// 关系型提供程序由 <c>HasTranslation</c> 翻译成 SQL，不会执行此方法体；
    /// 当 LINQ 提供程序不做翻译时（如单元测试的 InMemoryRepository），才会执行此处做内存求值。
    /// </remarks>
    public static bool JsonbArrayContains(object? jsonbColumn, string? element)
    {
        // 仅当 LINQ 提供程序不做翻译时（单元测试的 InMemoryRepository）才执行此方法体；
        // 关系型提供程序走 HasTranslation 翻译为 `col @> jsonb_build_array(elem)`，不会走到这里。
        if (element is null)
        {
            return false;
        }
        return jsonbColumn is IEnumerable<string> list && list.Contains(element);
    }
}
