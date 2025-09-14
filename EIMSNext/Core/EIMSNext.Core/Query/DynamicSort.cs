namespace EIMSNext.Core.Query
{
    public class DynamicSort
    {
        public DynamicSort() { }
        public DynamicSort(string field, SortDir dir = SortDir.Asc)
        {
            Field = field;
            Dir = dir;
        }

        public string Field { get; set; } = "";
        public SortDir Dir { get; set; } = SortDir.Asc;

        public static DynamicSort Create(string field, SortDir dir = SortDir.Asc)
        {
            return new DynamicSort(field, dir);
        }
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
