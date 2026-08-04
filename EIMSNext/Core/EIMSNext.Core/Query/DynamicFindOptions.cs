namespace EIMSNext.Core.Query
{
    public class DynamicFindOptions<T>
    {
        public const int DefaultTakeWhenUnspecified = 200;

        public DynamicFieldList? Select { get; set; }
        public DynamicFilter? Filter { get; set; }
        public DynamicSortList? Sort { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;

        public DataScope? Scope { get; set; }

        public string? Keyword { get; set; }

        public List<string>? SearchFields { get; set; }

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

    public class DataScope
    {
        public string? AuthGroupId { get; set; }

        public string? FormId { get; set; }

        public bool InheritMemberPermissions { get; set; }
    }
}
