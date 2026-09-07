using EIMSNext.Common;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态查询排序字段。
    /// </summary>
    public class DynamicSort
    {
        /// <summary>
        /// 初始化 <see cref="DynamicSort"/> 类的新实例。
        /// </summary>
        public DynamicSort() { }

        /// <summary>字段路径。</summary>
        public string Field { get; set; } = "";
        /// <summary>动态字段类型。</summary>
        public string? Type { get; set; }
        /// <summary>排序方向，1 为升序，-1 为降序。</summary>
        public SortDir Dir { get; set; } = SortDir.Asc;
    }
    /// <summary>
    /// 动态查询排序字段列表。
    /// </summary>
    public class DynamicSortList : List<DynamicSort>
    {

    }

    /// <summary>
    /// 排序方向。
    /// </summary>
    public enum SortDir
    {
        /// <summary>
        /// 升序。
        /// </summary>
        Asc = 1,
        /// <summary>
        /// 降序。
        /// </summary>
        Desc = -1
    }
}
