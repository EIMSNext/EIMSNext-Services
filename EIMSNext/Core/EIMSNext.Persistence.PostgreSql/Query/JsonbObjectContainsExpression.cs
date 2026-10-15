using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// 表示 PostgreSQL 的 <c>jsonb @&gt; jsonb 对象</c> 运算符节点，
/// 用于精确查找（<c>FilterOp.Exact</c>）的等值判定，可命中 jsonb_path_ops GIN 索引。
/// </summary>
/// <remarks>
/// 与 <see cref="JsonbArrayContainsExpression"/> 同理：jsonb_path_ops GIN 只服务
/// <c>@&gt;</c> 运算符（Eq/Text 使用的 <c>@?</c> 不命中）。右侧是按字段路径预构造的
/// jsonb 对象字面量（值已在 C# 侧按 JSON 规则转义，由 EF 以字面量渲染，无注入面）。
/// </remarks>
public sealed class JsonbObjectContainsExpression(SqlExpression jsonbColumn, SqlExpression jsonbObject)
    : SqlExpression(typeof(bool), BoolMapping)
{
    private static ConstructorInfo? _quotingConstructor;

    // 节点必须带 bool 类型映射：谓词会参与 AndAlso 等复合推断，TypeMapping 为 null 的
    // 自定义节点会在类型推断阶段被 EF 判成「无法翻译」。
    private static readonly BoolTypeMapping BoolMapping = new("boolean");

    public SqlExpression JsonbColumn { get; } = jsonbColumn;

    public SqlExpression JsonbObject { get; } = jsonbObject;

    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => Update((SqlExpression)visitor.Visit(JsonbColumn)!, (SqlExpression)visitor.Visit(JsonbObject)!);

    public JsonbObjectContainsExpression Update(SqlExpression jsonbColumn, SqlExpression jsonbObject)
        => jsonbColumn == JsonbColumn && jsonbObject == JsonbObject
            ? this
            : new JsonbObjectContainsExpression(jsonbColumn, jsonbObject);

    public override Expression Quote()
        => Expression.New(
            _quotingConstructor ??= typeof(JsonbObjectContainsExpression).GetConstructor(
                [typeof(SqlExpression), typeof(SqlExpression)])!,
            JsonbColumn.Quote(),
            JsonbObject.Quote());

    public override bool Equals(object? obj)
        => obj is JsonbObjectContainsExpression other
            && base.Equals(other)
            && JsonbColumn.Equals(other.JsonbColumn)
            && JsonbObject.Equals(other.JsonbObject);

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), JsonbColumn, JsonbObject);

    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Visit(JsonbColumn);
        expressionPrinter.Append(" @> ");
        expressionPrinter.Visit(JsonbObject);
    }

    public override string ToString() => $"{JsonbColumn} @> {JsonbObject}";
}
