namespace EIMSNext.Core.Query
{
    public class DynamicFindOptions<T>
    {
        public DynamicFieldList? Select { get; set; }
        public DynamicFilter? Filter { get; set; }
        public DynamicSortList? Sort { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;

        public DataScope? Scope { get; set; }

        public string? Keyword { get; set; }

        public List<string>? SearchFields { get; set; }

        public bool IncludeDeleted { get; set; }
    }

    public class DataScope
    {
        public string? AuthGroupId { get; set; }

        public string? FormId { get; set; }

        public bool InheritMemberPermissions { get; set; }
    }
}
