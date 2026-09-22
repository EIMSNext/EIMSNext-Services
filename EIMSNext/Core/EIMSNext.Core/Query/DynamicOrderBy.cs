namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态排序方向。
    /// </summary>
    public enum DynamicSortDirection
    {
        /// <summary>升序。</summary>
        Ascending = 0,

        /// <summary>降序。</summary>
        Descending = 1,
    }

    /// <summary>
    /// 动态排序条目。等价于原 Mongo 实现的 <c>SortDefinition&lt;T&gt;</c>，
    /// 通过字段名字符串表达排序，供 <see cref="DynamicQueryExtensions.OrderBy{T}"/> 翻译为 EF Core 排序。
    /// </summary>
    /// <param name="Field">排序字段名（已按字段类型做过后缀规整，如 <c>field.label</c>）。</param>
    /// <param name="Direction">排序方向。</param>
    public readonly record struct DynamicOrderBy(string Field, DynamicSortDirection Direction)
    {
        /// <summary>
        /// 创建升序排序条目。
        /// </summary>
        /// <param name="field">字段名。</param>
        /// <returns>排序条目。</returns>
        public static DynamicOrderBy Asc(string field) => new(field, DynamicSortDirection.Ascending);

        /// <summary>
        /// 创建降序排序条目。
        /// </summary>
        /// <param name="field">字段名。</param>
        /// <returns>排序条目。</returns>
        public static DynamicOrderBy Desc(string field) => new(field, DynamicSortDirection.Descending);
    }

    /// <summary>
    /// 动态排序列表。等价于原 Mongo 实现的 <c>SortDefinition&lt;T&gt;</c> 组合结果，
    /// 保留列表便于按顺序叠加多级排序。
    /// </summary>
    public sealed class DynamicSortDefinition
    {
        private readonly List<DynamicOrderBy> _items = [];

        /// <summary>
        /// 初始化 <see cref="DynamicSortDefinition"/> 类的新实例。
        /// </summary>
        public DynamicSortDefinition()
        {
        }

        /// <summary>
        /// 初始化 <see cref="DynamicSortDefinition"/> 类的新实例。
        /// </summary>
        /// <param name="items">排序条目集合。</param>
        public DynamicSortDefinition(IEnumerable<DynamicOrderBy> items)
        {
            _items.AddRange(items);
        }

        /// <summary>
        /// 获取排序条目集合。
        /// </summary>
        public IReadOnlyList<DynamicOrderBy> Items => _items;

        /// <summary>
        /// 获取一个值，指示是否没有任何排序条目。
        /// </summary>
        public bool IsEmpty => _items.Count == 0;

        /// <summary>
        /// 追加一个排序条目。已存在同字段时以最后一次为准（后者覆盖），
        /// 避免生成的 SQL 里出现重复的 ORDER BY 列。
        /// </summary>
        /// <param name="item">排序条目。</param>
        /// <returns>当前实例，便于链式调用。</returns>
        public DynamicSortDefinition Then(DynamicOrderBy item)
        {
            _items.RemoveAll(x => string.Equals(x.Field, item.Field, StringComparison.OrdinalIgnoreCase));
            _items.Add(item);
            return this;
        }
    }
}
