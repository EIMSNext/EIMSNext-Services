using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// 表示 PostgreSQL 的 <c>jsonb @? jsonpath</c> 运算符节点。
/// </summary>
/// <remarks>
/// <para>
/// 为什么需要自定义节点：GIN 索引（jsonb_ops / jsonb_path_ops）只能服务
/// <c>@&gt;</c>、<c>?</c>、<c>@@</c>、<c>@?</c> 这些<b>运算符</b>谓词；
/// <c>jsonb_path_exists(data, path)</c> 是函数调用，优化器不会把它当成索引条件，
/// 实测（PostgreSQL 18，enable_seqscan=off）只能是 Seq Scan，而同样的路径写成
/// <c>data @? path</c> 就能命中 <c>IX_FormData_Data_Gin</c>（Bitmap Index Scan）。
/// </para>
/// <para>
/// EF Core 的表达式树无法表达自定义中缀运算符，Npgsql 的
/// <c>PgBinaryExpression</c> 又是 internal，因此按 Npgsql 自家
/// <c>PgJsonTraversalExpression</c> 的做法实现一个 SqlExpression 子类：
/// 未知节点经 <c>VisitExtension</c> 回落到 <c>VisitChildren</c> 递归
/// （参数内联等优化器可正常下钻），SQL 生成本身由
/// <see cref="EimsNpgsqlQuerySqlGenerator"/> 完成。
/// </para>
/// </remarks>
public sealed class JsonPathExistsExpression(SqlExpression json, SqlExpression jsonPath)
    : SqlExpression(typeof(bool), BoolMapping)
{
    private static ConstructorInfo? _quotingConstructor;

    // 节点必须带 bool 类型映射：谓词会参与 AndAlso 等复合推断，TypeMapping 为 null 的
    // 自定义节点会在类型推断阶段被 EF 判成「无法翻译」。
    private static readonly BoolTypeMapping BoolMapping = new("boolean");

    public SqlExpression Json { get; } = json;

    public SqlExpression JsonPath { get; } = jsonPath;

    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => Update((SqlExpression)visitor.Visit(Json)!, (SqlExpression)visitor.Visit(JsonPath)!);

    public JsonPathExistsExpression Update(SqlExpression json, SqlExpression jsonPath)
        => json == Json && jsonPath == JsonPath ? this : new JsonPathExistsExpression(json, jsonPath);

    public override Expression Quote()
        => Expression.New(
            _quotingConstructor ??= typeof(JsonPathExistsExpression).GetConstructor(
                [typeof(SqlExpression), typeof(SqlExpression)])!,
            Json.Quote(),
            JsonPath.Quote());

    public override bool Equals(object? obj)
        => obj is JsonPathExistsExpression other
            && base.Equals(other)
            && Json.Equals(other.Json)
            && JsonPath.Equals(other.JsonPath);

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Json, JsonPath);

    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(Json);
        expressionPrinter.Append(" @? ");
        expressionPrinter.Visit(JsonPath);
    }

    public override string ToString() => $"{Json} @? {JsonPath}";
}
