using EIMSNext.Common;
using EIMSNext.Core.Query;

namespace EIMSNext.ApiService.RequestModels
{
    public class AggCalcRequest
    {
        public required AgDataSource DataSource { get; set; }
        public List<Dimension>? Dimensions { get; set; }
        public List<Metric>? Metrics { get; set; }
        public DynamicFilter? Filter { get; set; }
        public List<SortItem>? Sort { get; set; }
        public int? Take { get; set; }
    }

    public class AgDataSource
    {
        public string Id { get; set; } = string.Empty;
        public AgDataSourceType Type { get; set; }
    }
    public class Dimension
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = FieldType.Input;
    }
    public class Metric
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = FieldType.Input;
        public string AggFun { get; set; } = "count";
    }
    public class SortItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = FieldType.Number;
        public SortDir Dir { get; set; }
    }

    public enum AgDataSourceType
    {
        Form
    }
}
