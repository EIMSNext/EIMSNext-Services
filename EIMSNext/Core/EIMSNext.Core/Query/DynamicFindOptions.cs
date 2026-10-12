namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态表单查询选项。表单字段路径由对应 FormDef 的 content.items 决定。
    /// </summary>
    public class DynamicFindOptions<T>
    {
        /// <summary>动态筛选条件或条件组。</summary>
        public DynamicFilter? Filter { get; set; }
        /// <summary>排序字段列表。</summary>
        public DynamicSortList? Sort { get; set; }
        /// <summary>查询偏移量，负数归一化为 0。</summary>
        public int Skip { get; set; }

        /// <summary>
        /// 单页数量。0（或负数）表示不限量，由调用方自行控制结果规模。
        /// </summary>
        /// <remarks>
        /// 请求入口不得直接使用这个默认值 —— 客户端不传分页参数时会退化成整表拉取。
        /// 来自请求的实例必须先过 <see cref="RequestPagingPolicy.Normalize"/>。
        /// </remarks>
        public int Take { get; set; }

        /// <summary>数据权限作用域。</summary>
        public DataScope? Scope { get; set; }

        /// <summary>关键字搜索文本。</summary>
        public string? Keyword { get; set; }

        /// <summary>参与关键字搜索的动态字段名。</summary>
        public List<string>? SearchFields { get; set; }

        /// <summary>是否包含逻辑删除数据。</summary>
        public bool IncludeDeleted { get; set; }

        /// <summary>
        /// 获取有效的返回记录数。
        /// </summary>
        /// <returns>负数归一化为 0；0 表示不限量，不会生成 LIMIT。</returns>
        public int GetEffectiveTake()
        {
            return Math.Max(0, Take);
        }

        /// <summary>
        /// 获取有效的跳过记录数。
        /// </summary>
        /// <returns>有效的跳过记录数。</returns>
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
        public string? PermissionGroupId { get; set; }

        /// <summary>目标表单 ID。</summary>
        public string? FormId { get; set; }

        /// <summary>是否继承成员权限。</summary>
        public bool InheritMemberPermissions { get; set; }
    }
}
