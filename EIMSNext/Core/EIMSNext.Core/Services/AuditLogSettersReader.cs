using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Query;

// ExecuteUpdate 的 setters 只能由 EF Core 的 UpdateSettersBuilder 还原；该 API 标记为 internal，
// 升级 EF Core 时需回归本解析器。解析失败会静默降级为 []，不影响业务写入。
#pragma warning disable EF1001

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 从 EF Core ExecuteUpdate 的 setters 委托中提取批量更新字段，生成 AuditLog.Detail 数组。
    /// </summary>
    public static class AuditLogSettersReader
    {
        /// <summary>
        /// 重放 setters 委托，返回 [{field,type,label,old,new}] JSON 数组。
        /// 批量更新不加载实体，old 恒为 null；解析失败时返回空数组，不影响业务写入。
        /// </summary>
        public static string Build<T>(Action<UpdateSettersBuilder<T>>? setters) where T : class
        {
            if (setters is null) return AuditLogChangeBuilder.EmptyChanges;

            try
            {
                var builder = new UpdateSettersBuilder<T>();
                setters(builder);
                var array = builder.BuildSettersExpression();
                var changes = new List<AuditLogChange>();

                foreach (var setter in array.Expressions)
                {
                    if (setter is not NewExpression tuple || tuple.Arguments.Count < 2) continue;

                    var member = MemberName(tuple.Arguments[0]);
                    if (string.IsNullOrEmpty(member) || AuditLogChangeBuilder.IsSystemField(member)) continue;
                    var field = char.ToLowerInvariant(member[0]) + member[1..];

                    var value = ReadValue(tuple.Arguments[1]);
                    changes.Add(new AuditLogChange
                    {
                        Field = field,
                        Type = AuditLogChangeBuilder.TypeOf(field, value),
                        Label = AuditLogChangeBuilder.LabelOf(field),
                        Old = null,
                        New = AuditLogChangeBuilder.Normalize(value, field),
                    });

                    if (changes.Count >= AuditLogChangeBuilder.MaxItems) break;
                }

                return AuditLogChangeBuilder.Serialize(changes);
            }
            catch
            {
                return AuditLogChangeBuilder.EmptyChanges;
            }
        }

        private static string? MemberName(Expression expression) => expression switch
        {
            UnaryExpression unary => MemberName(unary.Operand),
            LambdaExpression lambda => MemberName(lambda.Body),
            MemberExpression member => member.Member.Name,
            _ => null,
        };

        private static object? ReadValue(Expression expression)
        {
            if (expression is UnaryExpression unary) return ReadValue(unary.Operand);
            if (expression is ConstantExpression constant) return constant.Value;
            // 依赖当前行旧值的表达式（如 x => x.VisitCount + 1）无法求值，只记录表达式文本。
            if (expression is LambdaExpression lambda) return lambda.Body.ToString();
            if (expression is BinaryExpression or MethodCallExpression) return expression.ToString();

            try
            {
                return Expression.Lambda(expression).Compile().DynamicInvoke();
            }
            catch
            {
                return expression.ToString();
            }
        }
    }
}

#pragma warning restore EF1001
