using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// 在 Npgsql 的 SQL 生成器上补一个节点：输出 <c>json @? jsonpath</c>。
/// </summary>
/// <remarks>
/// <see cref="JsonPathExistsExpression"/> 由 <c>EIMSNextModelConfiguration</c> 里的
/// <c>HasDbFunction(...).HasTranslation(...)</c> 生成；EF 内置的 QuerySqlGenerator
/// 不认识这个自定义节点，因此这里继承 Npgsql 的生成器补上分发。
/// </remarks>
public sealed class EimsNpgsqlQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies,
    IRelationalTypeMappingSource typeMappingSource,
    bool reverseNullOrderingEnabled,
    Version? postgresVersion)
    : NpgsqlQuerySqlGenerator(dependencies, typeMappingSource, reverseNullOrderingEnabled, postgresVersion)
{
    protected override Expression VisitExtension(Expression extensionExpression)
        => extensionExpression is JsonPathExistsExpression jsonPathExists
            ? VisitJsonPathExists(jsonPathExists)
            : base.VisitExtension(extensionExpression);

    private Expression VisitJsonPathExists(JsonPathExistsExpression jsonPathExists)
    {
        Visit((Expression)jsonPathExists.Json);
        Sql.Append(" @? ");
        Visit((Expression)jsonPathExists.JsonPath);
        return jsonPathExists;
    }
}

public sealed class EimsNpgsqlQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies,
    IRelationalTypeMappingSource typeMappingSource,
    INpgsqlSingletonOptions npgsqlSingletonOptions) : IQuerySqlGeneratorFactory
{
    public QuerySqlGenerator Create()
        => new EimsNpgsqlQuerySqlGenerator(
            dependencies,
            typeMappingSource,
            npgsqlSingletonOptions.ReverseNullOrderingEnabled,
            npgsqlSingletonOptions.PostgresVersion);
}

/// <summary>
/// 参数空值处理：EF 的 SqlNullabilityProcessor 不认识自定义节点，这里补上
/// <see cref="JsonPathExistsExpression"/> 的递归访问与可空性计算
/// （<c>@?</c> 的可空性与左操作数一致：Data 为 NULL 时谓词为 NULL，即不命中）。
/// </summary>
public sealed class EimsNpgsqlSqlNullabilityProcessor(
    RelationalParameterBasedSqlProcessorDependencies dependencies,
    RelationalParameterBasedSqlProcessorParameters parameters)
    : NpgsqlSqlNullabilityProcessor(dependencies, parameters)
{
    protected override SqlExpression VisitCustomSqlExpression(
        SqlExpression sqlExpression,
        bool allowOptimizedExpansion,
        out bool nullable)
        => sqlExpression is JsonPathExistsExpression jsonPathExists
            ? VisitJsonPathExists(jsonPathExists, allowOptimizedExpansion, out nullable)
            : base.VisitCustomSqlExpression(sqlExpression, allowOptimizedExpansion, out nullable);

    private SqlExpression VisitJsonPathExists(
        JsonPathExistsExpression jsonPathExists,
        bool allowOptimizedExpansion,
        out bool nullable)
    {
        var json = Visit(jsonPathExists.Json, allowOptimizedExpansion, out var jsonNullable);
        var jsonPath = Visit(jsonPathExists.JsonPath, allowOptimizedExpansion, out _);
        nullable = jsonNullable;
        return jsonPathExists.Update(json, jsonPath);
    }
}

public sealed class EimsNpgsqlParameterBasedSqlProcessor(
    RelationalParameterBasedSqlProcessorDependencies dependencies,
    RelationalParameterBasedSqlProcessorParameters parameters)
    : NpgsqlParameterBasedSqlProcessor(dependencies, parameters)
{
    protected override Expression ProcessSqlNullability(
        Expression selectExpression,
        ParametersCacheDecorator parametersDecorator)
        => new EimsNpgsqlSqlNullabilityProcessor(Dependencies, Parameters).Process(selectExpression, parametersDecorator);
}

public sealed class EimsNpgsqlParameterBasedSqlProcessorFactory(
    RelationalParameterBasedSqlProcessorDependencies dependencies)
    : NpgsqlParameterBasedSqlProcessorFactory(dependencies)
{
    public override RelationalParameterBasedSqlProcessor Create(RelationalParameterBasedSqlProcessorParameters parameters)
        => new EimsNpgsqlParameterBasedSqlProcessor(dependencies, parameters);
}

/// <summary>
/// jsonb 过滤谓词的 SQL 渲染接线：让 <see cref="JsonPathExistsExpression"/> 输出为
/// <c>@?</c> 运算符，使 <c>IX_FormData_Data_Gin</c> 等 GIN 索引可用。
/// </summary>
/// <remarks>泛型签名是为了在 <c>DbContextOptionsBuilder&lt;TContext&gt;</c> 上调用后仍拿到强类型 Options。</remarks>
public static class EimsJsonPathTranslationExtensions
{
    public static TBuilder UseEimsJsonPathOperators<TBuilder>(this TBuilder optionsBuilder)
        where TBuilder : DbContextOptionsBuilder
        => (TBuilder)optionsBuilder
            .ReplaceService<IQuerySqlGeneratorFactory, EimsNpgsqlQuerySqlGeneratorFactory>()
            .ReplaceService<IRelationalParameterBasedSqlProcessorFactory, EimsNpgsqlParameterBasedSqlProcessorFactory>();
}
