using System.Collections;
using EIMSNext.Common;

namespace EIMSNext.Core.Query;

/// <summary>动态过滤条件的参数规则和无效条件归一化。</summary>
public static class DynamicFilterRules
{
    private static readonly HashSet<string> ValueOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        FilterOp.Eq, FilterOp.Ne, FilterOp.Gt, FilterOp.Gte, FilterOp.Lt, FilterOp.Lte,
        FilterOp.In, FilterOp.Nin, FilterOp.AllIn, FilterOp.Between, FilterOp.Text,
    };

    /// <summary>移除缺少比较值的条件；allin 空数组保留并由翻译器生成 false。</summary>
    public static DynamicFilter? Normalize(DynamicFilter? filter)
    {
        if (filter is null) return null;
        if (filter.IsGroup)
        {
            var items = (filter.Items ?? []).Select(Normalize).Where(x => x is not null).Cast<DynamicFilter>().ToList();
            return items.Count == 0 ? null : new DynamicFilter { Rel = filter.Rel, Items = items };
        }

        if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Op)) return null;
        var op = filter.Op.ToLowerInvariant();
        if (!ValueOperators.Contains(op)) return Clone(filter);

        var values = ToValues(filter.Value);
        if (op == FilterOp.AllIn) return Clone(filter, values, replaceValue: true);
        if (op == FilterOp.Text && (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]?.ToString()))) return null;
        if (values.Count == 0 || op == FilterOp.Between && values.Count < 2) return null;
        return Clone(filter, values, replaceValue: true);
    }

    /// <summary>把比较值归一化为值列表。</summary>
    public static List<object?> ToValues(object? value)
    {
        var normalized = DynamicValueNormalizer.Normalize(value);
        if (normalized is null) return [];
        if (normalized is string) return [normalized];
        if (normalized is IDictionary) return [normalized];
        if (normalized is IEnumerable enumerable)
            return enumerable.Cast<object?>().Select(DynamicValueNormalizer.Normalize).Where(x => x is not null).ToList();
        return [normalized];
    }

    private static DynamicFilter Clone(DynamicFilter source, object? value = null, bool replaceValue = false) => new()
    {
        Rel = source.Rel,
        Field = source.Field,
        Type = source.Type,
        Op = source.Op,
        Value = replaceValue ? value : source.Value,
        ValueIsExp = source.ValueIsExp,
        ValueIsField = source.ValueIsField,
    };
}
