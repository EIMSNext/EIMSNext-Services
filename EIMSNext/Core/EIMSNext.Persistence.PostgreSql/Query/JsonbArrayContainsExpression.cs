using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// 表示 PostgreSQL 的 <c>jsonb @&gt; jsonb_build_array(element)</c> 运算符节点，
/// 用于 jsonb 数组列「包含某元素」的判定，可命中 jsonb_path_ops GIN 索引。
/// </summary>
/// <remarks>
/// 与 <see cref="JsonPathExistsExpression"/> 同理：GIN 索引（jsonb_path_ops）只服务
/// <c>@&gt;</c> 这类<b>运算符</b>谓词；函数调用（如 <c>jsonb_array_elements</c>）无法走索引。
/// EF Core 表达式树无法直接表达自定义中缀运算符（Npgsql 的
/// <c>PgBinaryExpression</c> 是 internal），故实现为 <see cref="SqlExpression"/> 子类，
/// 由 <see cref="EimsNpgsqlQuerySqlGenerator"/> 渲染成 <c>@&gt;</c>。
/// 右侧用 <c>jsonb_build_array(element)</c> 而非字符串拼接字面量：既避免注入，
/// 又把右侧构造成数组常量，GIN 在左侧 jsonb 列上仍可被优化器利用。
/// </remarks>
public sealed class JsonbArrayContainsExpression(SqlExpression jsonbColumn, SqlExpression element)
    : SqlExpression(typeof(bool), BoolMapping)
{
    private static ConstructorInfo? _quotingConstructor;

    // 节点必须带 bool 类型映射：谓词会参与 AndAlso 等复合推断，TypeMapping 为 null 的
    // 自定义节点会在类型推断阶段被 EF 判成「无法翻译」。
    private static readonly BoolTypeMapping BoolMapping = new("boolean");

    public SqlExpression JsonbColumn { get; } = jsonbColumn;

    public SqlExpression Element { get; } = element;

    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => Update((SqlExpression)visitor.Visit(JsonbColumn)!, (SqlExpression)visitor.Visit(Element)!);

    public JsonbArrayContainsExpression Update(SqlExpression jsonbColumn, SqlExpression element)
        => jsonbColumn == JsonbColumn && element == Element
            ? this
            : new JsonbArrayContainsExpression(jsonbColumn, element);

    public override Expression Quote()
        => Expression.New(
            _quotingConstructor ??= typeof(JsonbArrayContainsExpression).GetConstructor(
                [typeof(SqlExpression), typeof(SqlExpression)])!,
            JsonbColumn.Quote(),
            Element.Quote());

    public override bool Equals(object? obj)
        => obj is JsonbArrayContainsExpression other
            && base.Equals(other)
            && JsonbColumn.Equals(other.JsonbColumn)
            && Element.Equals(other.Element);

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), JsonbColumn, Element);

    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(JsonbColumn);
        expressionPrinter.Append(" @> jsonb_build_array(");
        expressionPrinter.Visit(Element);
        expressionPrinter.Append(")");
    }

    public override string ToString() => $"{JsonbColumn} @> jsonb_build_array({Element})";
}
