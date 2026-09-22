using EIMSNext.Common;

namespace EIMSNext.Core.Query;

/// <summary>
/// 校验动态过滤树，确保非法条件不会被翻译器静默丢弃而退化成全量查询。
/// </summary>
public static class DynamicFilterValidator
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        FilterOp.Eq,
        FilterOp.Exists,
        FilterOp.Gt,
        FilterOp.Gte,
        FilterOp.In,
        FilterOp.AllIn,
        FilterOp.Lt,
        FilterOp.Lte,
        FilterOp.Between,
        FilterOp.Ne,
        FilterOp.Nin,
        FilterOp.Text,
        FilterOp.Empty,
        FilterOp.NotEmpty,
    };

    /// <summary>验证过滤树；非法运算符或空子条件会抛出请求错误。</summary>
    public static void Validate(DynamicFilter? filter)
        => Validate(filter, allowEmpty: true);

    private static void Validate(DynamicFilter? filter, bool allowEmpty)
    {
        if (filter is null) return;
        if (filter.IsEmpty && string.IsNullOrWhiteSpace(filter.Field) && string.IsNullOrWhiteSpace(filter.Op))
        {
            if (allowEmpty) return;
            throw new BadRequestException("过滤条件不能为空");
        }

        if (filter.IsGroup)
        {
            if (!string.Equals(filter.Rel, FilterRel.And, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(filter.Rel, FilterRel.Or, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(filter.Rel, FilterRel.Not, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException($"不支持的过滤关系: {filter.Rel}");
            }

            foreach (var child in filter.Items ?? [])
            {
                if (child is null) throw new BadRequestException("过滤条件不能为空");
                Validate(child, allowEmpty: false);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(filter.Field))
            throw new BadRequestException("过滤字段不能为空");
        if (string.IsNullOrWhiteSpace(filter.Op))
            throw new BadRequestException("过滤运算符不能为空");
        if (!SupportedOperators.Contains(filter.Op))
            throw new BadRequestException($"不支持的过滤运算符: {filter.Op}");
    }
}
