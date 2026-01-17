using EIMSNext.Common;

namespace EIMSNext.Core.Query
{
    public class DynamicSort
    {
        public DynamicSort() { }

        public string Field { get; set; } = "";
        public string? Type { get; set; }
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
