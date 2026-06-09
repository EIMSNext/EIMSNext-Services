using EIMSNext.Core.Query;

namespace EIMSNext.ApiService.RequestModels
{
    public class FormDataFilterOptionsRequest
    {
        public string FormId { get; set; } = string.Empty;

        public string Field { get; set; } = string.Empty;

        public string? FieldType { get; set; }

        public string? Keyword { get; set; }

        public DynamicFilter? Filter { get; set; }

        public int Limit { get; set; } = 50;
    }

    public class FormDataFilterOptionsResponse
    {
        public List<FilterOptionItem> Items { get; set; } = [];
    }
}
