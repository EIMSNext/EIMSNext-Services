namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态表单查询选项。表单字段路径由对应 FormDef 的 content.items 决定。
    /// </summary>
    public class DynamicFindOptions<T>
    {
        public const int DefaultTakeWhenUnspecified = 200;

        /// <summary>投影字段列表。</summary>
        public DynamicFieldList? Select { get; set; }
        /// <summary>动态筛选条件或条件组。</summary>
        public DynamicFilter? Filter { get; set; }
        /// <summary>排序字段列表。</summary>
        public DynamicSortList? Sort { get; set; }
        /// <summary>查询偏移量，负数归一化为 0。</summary>
        public int Skip { get; set; }
        /// <summary>单页数量，默认 20；小于等于 0 时使用服务端默认 200。</summary>
        public int Take { get; set; } = 20;

        /// <summary>数据权限作用域。</summary>
        public DataScope? Scope { get; set; }

        /// <summary>关键字搜索文本。</summary>
        public string? Keyword { get; set; }

        /// <summary>参与关键字搜索的动态字段名。</summary>
        public List<string>? SearchFields { get; set; }

        /// <summary>是否包含逻辑删除数据。</summary>
        public bool IncludeDeleted { get; set; }

        /// <summary>
        /// MongoDB treats a limit of zero as unlimited. Dynamic callers use zero to
        /// mean "use the server default", so normalize it before executing a query.
        /// </summary>
        public int GetEffectiveTake()
        {
            return Take <= 0 ? DefaultTakeWhenUnspecified : Take;
        }

        public int GetEffectiveSkip()
        {
            return Math.Max(0, Skip);
        }
    }

    /// <summary>
    /// 动态查询的数据权限作用域。
    /// </summary>
    public class DataScope
    {
        /// <summary>数据权限组 ID。</summary>
        public string? AuthGroupId { get; set; }

        /// <summary>目标表单 ID。</summary>
        public string? FormId { get; set; }

        /// <summary>是否继承成员权限。</summary>
        public bool InheritMemberPermissions { get; set; }
    }
}
