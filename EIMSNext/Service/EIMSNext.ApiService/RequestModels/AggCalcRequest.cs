using EIMSNext.Common;
using EIMSNext.Core.Query;

namespace EIMSNext.ApiService.RequestModels
{
    /// <summary>
    /// 聚合计算请求
    /// </summary>
    public class AggCalcRequest
    {
        /// <summary>
        /// 数据源
        /// </summary>
        public required AgDataSource DataSource { get; set; }
        /// <summary>
        /// 维度列表
        /// </summary>
        public List<Dimension>? Dimensions { get; set; }
        /// <summary>
        /// 指标列表
        /// </summary>
        public List<Metric>? Metrics { get; set; }
        /// <summary>
        /// 过滤条件
        /// </summary>
        public DynamicFilter? Filter { get; set; }
        /// <summary>
        /// 排序列表
        /// </summary>
        public List<SortItem>? Sort { get; set; }
        /// <summary>
        /// 返回数量
        /// </summary>
        public int? Take { get; set; }
    }

    /// <summary>
    /// 聚合数据源
    /// </summary>
    public class AgDataSource
    {
        /// <summary>
        /// 数据源ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 数据源类型
        /// </summary>
        public AgDataSourceType Type { get; set; }
    }
    /// <summary>
    /// 维度定义
    /// </summary>
    public class Dimension
    {
        /// <summary>
        /// 字段ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 字段类型
        /// </summary>
        public string Type { get; set; } = FieldType.Input;
    }
    /// <summary>
    /// 指标定义
    /// </summary>
    public class Metric
    {
        /// <summary>
        /// 字段ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 字段类型
        /// </summary>
        public string Type { get; set; } = FieldType.Input;
        /// <summary>
        /// 聚合函数
        /// </summary>
        public string AggFun { get; set; } = "count";
    }
    /// <summary>
    /// 排序项
    /// </summary>
    public class SortItem
    {
        /// <summary>
        /// 字段ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// 字段类型
        /// </summary>
        public string Type { get; set; } = FieldType.Number;
        /// <summary>
        /// 排序方向
        /// </summary>
        public SortDir Dir { get; set; }
    }

    /// <summary>
    /// 聚合数据源类型
    /// </summary>
    public enum AgDataSourceType
    {
        /// <summary>
        /// 表单
        /// </summary>
        Form
    }
}
