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
        FilterOp.Exact,
        FilterOp.Empty,
        FilterOp.NotEmpty,
    };

    /// <summary>验证过滤树；非法运算符、空子条件或树规模超限会抛出请求错误。</summary>
    public static void Validate(DynamicFilter? filter)
        => Validate(filter, allowEmpty: true);

    // 过滤树来自客户端 JSON（OData $query / 公开 QueryLink 均可直接提交），不信任前端构建器的层级
    // 限制；超深的嵌套会让递归翻译器与各处遍历放大开销，节点数超限则拖垮谓词翻译。
    private const int MaxDepth = 5;
    private const int MaxNodeCount = 50;

    private static void Validate(DynamicFilter? filter, bool allowEmpty)
    {
        if (filter is null) return;
        if (filter.IsEmpty && string.IsNullOrWhiteSpace(filter.Field) && string.IsNullOrWhiteSpace(filter.Op))
        {
            if (allowEmpty) return;
            throw new BadRequestException("过滤条件不能为空");
        }

        EnsureTreeSize(filter);

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

        if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Op))
            return;
        if (!SupportedOperators.Contains(filter.Op))
            throw new BadRequestException($"不支持的过滤运算符: {filter.Op}");
    }

    private static void EnsureTreeSize(DynamicFilter? filter)
    {
        var nodeCount = 0;
        Walk(filter, 1);
        return;

        void Walk(DynamicFilter? node, int depth)
        {
            if (node is null) return;
            if (++nodeCount > MaxNodeCount)
            {
                throw new BadRequestException($"过滤条件节点数超过上限 {MaxNodeCount}");
            }

            if (!node.IsGroup) return;

            if (depth > MaxDepth)
            {
                throw new BadRequestException($"过滤条件嵌套层级超过上限 {MaxDepth}");
            }

            foreach (var child in node.Items ?? [])
            {
                Walk(child, depth + 1);
            }
        }
    }
}
