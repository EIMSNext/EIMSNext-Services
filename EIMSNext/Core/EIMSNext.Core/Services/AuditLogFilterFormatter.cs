using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Services
{
    /// <summary>
    /// 把批量更新/删除条件渲染成可读文本，替代表达式默认的 value(DisplayClass).field 闭包引用。
    /// </summary>
    public static class AuditLogFilterFormatter
    {
        private const int MaxCollectionItems = 20;
        private const int MaxScalarLength = 200;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        /// <summary>求值谓词中的闭包引用，输出带实际值的条件文本；失败时退回原始表达式文本。</summary>
        public static string Format(Expression? filter)
        {
            if (filter is null) return string.Empty;

            var text = filter.ToString();
            try
            {
                var collector = new MemberCollector();
                collector.Visit(filter);

                var members = collector.Members
                    .Select(x => (Key: x.ToString(), Member: x))
                    .GroupBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => x.First())
                    .OrderByDescending(x => x.Key.Length);

                foreach (var item in members)
                {
                    object? value;
                    try
                    {
                        // 含实体参数的成员（如 x.Id）无法独立求值，DynamicInvoke 会抛异常并跳过。
                        value = Expression.Lambda(item.Member).Compile().DynamicInvoke();
                    }
                    catch
                    {
                        continue;
                    }

                    text = text.Replace(item.Key, FormatValue(value), StringComparison.Ordinal);
                }

                return text;
            }
            catch
            {
                return text;
            }
        }

        /// <summary>动态条件按结构序列化，避免只落一个类型名。</summary>
        public static string Format(DynamicFilter? filter)
            => filter is null ? string.Empty : JsonSerializer.Serialize(filter, JsonOptions);

        private static string FormatValue(object? value) => value switch
        {
            null => "null",
            string text => Quote(text),
            bool flag => flag ? "True" : "False",
            IEnumerable items => FormatCollection(items),
            IFormattable formattable => Clamp(formattable.ToString(null, CultureInfo.InvariantCulture)),
            _ => Clamp(value.ToString()),
        };

        private static string FormatCollection(IEnumerable items)
        {
            var parts = new List<string>();
            var count = 0;
            foreach (var item in items)
            {
                count++;
                if (parts.Count < MaxCollectionItems) parts.Add(FormatValue(item));
            }

            var text = string.Join(", ", parts);
            return count > parts.Count ? $"[{text}, …（共 {count} 项）]" : $"[{text}]";
        }

        private static string Quote(string value)
            => "\"" + Clamp(value).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string Clamp(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= MaxScalarLength ? value : value[..MaxScalarLength] + "…";
        }

        private sealed class MemberCollector : ExpressionVisitor
        {
            public List<MemberExpression> Members { get; } = [];

            protected override Expression VisitMember(MemberExpression node)
            {
                Members.Add(node);
                return base.VisitMember(node);
            }
        }
    }
}
