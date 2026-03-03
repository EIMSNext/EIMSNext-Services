using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace EIMSNext.ApiService.RequestModel
{
    public class AggCalcRequest
    {
        public AgDataSource? DataSource { get; set; }
        public List<Dimension>? Dimensions { get; set; }
        public List<Metric>? Metrics { get; set; }
        public List<AgFilter>? Filter { get; set; }
    }

    public class AgDataSource
    {
        public string Id { get; set; }
        public AgDataSourceType Type { get; set; }
    }
    public class Dimension
    {
        public string Id { get; set; }
    }
    public class Metric
    {
        public string Id { get; set; }
        public string AgFun { get; set; }
    }
    public class AgFilter
    {
        public string Field { get; set; } 
        public string Operator { get; set; } 
        public BsonValue Value { get; set; }
    }

    public enum AgDataSourceType
    {
        Form
    }
}
