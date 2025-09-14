namespace EIMSNext.Core.Query
{
    public class DynamicFindOptions<T>
    {
        public DynamicFieldList? Select { get; set; }
        public DynamicFilter? Filter { get; set; }
        public DynamicSortList? Sort { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
    }
}
