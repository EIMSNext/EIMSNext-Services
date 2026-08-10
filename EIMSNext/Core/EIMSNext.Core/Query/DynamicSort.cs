using EIMSNext.Common;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态查询排序字段。
    /// </summary>
    public class DynamicSort
    {
        public DynamicSort() { }

        /// <summary>字段路径。</summary>
        public string Field { get; set; } = "";
        /// <summary>动态字段类型。</summary>
        public string? Type { get; set; }
        /// <summary>排序方向，1 为升序，-1 为降序。</summary>
        public SortDir Dir { get; set; } = SortDir.Asc;
    }
    public class DynamicSortList : List<DynamicSort>
    {

    }

    public enum SortDir
    {
        Asc = 1,
        Desc = -1
    }
}
