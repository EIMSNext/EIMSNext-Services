namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 过滤选项查询条件。
    /// </summary>
    public class FilterOptionQuery
    {
        /// <summary>
        /// 获取或设置动态过滤条件。
        /// </summary>
        public DynamicFilter Filter { get; set; } = new();

        /// <summary>
        /// 获取或设置要查询的字段路径。
        /// </summary>
        public string FieldPath { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置关键词。
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>
        /// 获取或设置返回数量上限。
        /// </summary>
        public int Limit { get; set; } = 50;
    }

    /// <summary>
    /// 过滤选项查询结果。
    /// </summary>
    public class FilterOptionResult
    {
        /// <summary>
        /// 获取或设置过滤选项列表。
        /// </summary>
        public List<FilterOptionItem> Items { get; set; } = [];
    }

    /// <summary>
    /// 过滤选项条目。
    /// </summary>
    public class FilterOptionItem
    {
        /// <summary>
        /// 获取或设置选项 ID。
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置选项显示标签。
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置选项值。
        /// </summary>
        public object? Value { get; set; }
    }
}
