namespace EIMSNext.Core.Query
{
    public class FilterOptionQuery
    {
        public DynamicFilter Filter { get; set; } = new();

        public string FieldPath { get; set; } = string.Empty;

        public string? Keyword { get; set; }

        public int Limit { get; set; } = 50;
    }

    public class FilterOptionResult
    {
        public List<FilterOptionItem> Items { get; set; } = [];
    }

    public class FilterOptionItem
    {
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public object? Value { get; set; }
    }
}
