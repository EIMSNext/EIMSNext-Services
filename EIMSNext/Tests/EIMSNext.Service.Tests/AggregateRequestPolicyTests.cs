using System.Text.Json;
using EIMSNext.ApiService;
using EIMSNext.ApiService.RequestModels;
using EIMSNext.Core.Query;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class AggregateRequestPolicyTests
    {
        [TestMethod]
        public void AggregateFunctions_RejectsMongoOperatorOutsideWhitelist()
        {
            var request = Request([], [new Metric { Id = "amount", AggFun = "push" }]);

            Assert.IsFalse(AggregateApiService.HasSupportedAggregateFunctions(request));
        }

        [TestMethod]
        public void ChartShape_RequiresConfiguredDimensionAndMetricsExactly()
        {
            using var details = Details("""
                {
                  "dimension1": [{ "id": "category" }],
                  "dimension2": [],
                  "metrics": [{ "id": "amount", "aggFun": "sum" }]
                }
                """);
            var valid = Request([new Dimension { Id = "category" }], [new Metric { Id = "amount", AggFun = "sum" }]);
            var extraMetric = Request([new Dimension { Id = "category" }], [new Metric { Id = "amount", AggFun = "sum" }, new Metric { Id = "secret", AggFun = "max" }]);
            var missingMetric = Request([new Dimension { Id = "category" }], []);

            Assert.IsTrue(AggregateApiService.IsConfiguredChartShapeValid(valid, details.RootElement));
            Assert.IsFalse(AggregateApiService.IsConfiguredChartShapeValid(extraMetric, details.RootElement));
            Assert.IsFalse(AggregateApiService.IsConfiguredChartShapeValid(missingMetric, details.RootElement));
        }

        [TestMethod]
        public void ProgressTargetMetric_IsRequiredOnlyInMetricMode()
        {
            using var metricMode = Details("""
                {
                  "dimension1": [],
                  "dimension2": [],
                  "metrics": [{ "id": "actual", "aggFun": "sum" }],
                  "progress": { "targetType": "metric", "targetMetric": { "id": "target", "aggFun": "max" } }
                }
                """);
            using var valueMode = Details("""
                {
                  "dimension1": [],
                  "dimension2": [],
                  "metrics": [{ "id": "actual", "aggFun": "sum" }],
                  "progress": { "targetType": "value", "targetValue": 100, "targetMetric": { "id": "stale", "aggFun": "max" } }
                }
                """);
            var metricRequest = Request([], [new Metric { Id = "actual", AggFun = "sum" }, new Metric { Id = "target", AggFun = "max" }]);
            var valueRequest = Request([], [new Metric { Id = "actual", AggFun = "sum" }]);

            Assert.IsTrue(AggregateApiService.IsConfiguredChartShapeValid(metricRequest, metricMode.RootElement));
            Assert.IsTrue(AggregateApiService.IsConfiguredChartShapeValid(valueRequest, valueMode.RootElement));
        }

        [TestMethod]
        public void ConfiguredFilter_MustRemainAndCanOnlyBeExtendedWithAnd()
        {
            using var details = Details("""
                {
                  "filter": {
                    "id": "filter-1",
                    "field": { "formId": "form-1", "field": "status", "type": "radio" },
                    "op": "eq",
                    "value": { "type": "custom", "value": "approved" }
                  }
                }
                """);
            var configured = new DynamicFilter { Field = "data.status", Type = "radio", Op = "eq", Value = "approved" };
            var extended = new DynamicFilter
            {
                Rel = FilterRel.And,
                Items = [configured, new DynamicFilter { Field = "data.department", Op = "eq", Value = "sales" }],
            };
            var weakened = new DynamicFilter
            {
                Rel = FilterRel.Or,
                Items = [configured, new DynamicFilter { Field = "data.department", Op = "eq", Value = "sales" }],
            };

            Assert.IsTrue(AggregateApiService.ContainsConfiguredFilter(configured, details.RootElement));
            Assert.IsTrue(AggregateApiService.ContainsConfiguredFilter(extended, details.RootElement));
            Assert.IsFalse(AggregateApiService.ContainsConfiguredFilter(weakened, details.RootElement));
            Assert.IsFalse(AggregateApiService.ContainsConfiguredFilter(null, details.RootElement));
        }

        [TestMethod]
        public void ClearValueExpressions_ResetsEveryFilterInTheTree()
        {
            var filter = new DynamicFilter
            {
                ValueIsExp = true,
                ValueIsField = true,
                Items =
                [
                    new DynamicFilter { Field = "data.status", ValueIsExp = true },
                    new DynamicFilter
                    {
                        Items =
                        [
                            new DynamicFilter { Field = "data.owner", ValueIsField = true },
                        ],
                    },
                ],
            };

            filter.ClearValueExpressions();

            Assert.IsFalse(filter.ValueIsExp);
            Assert.IsFalse(filter.ValueIsField);
            Assert.IsFalse(filter.Items[0].ValueIsExp);
            Assert.IsFalse(filter.Items[1].Items![0].ValueIsField);
        }

        private static JsonDocument Details(string json) => JsonDocument.Parse(json);

        private static AggCalcRequest Request(List<Dimension> dimensions, List<Metric> metrics) => new()
        {
            DataSource = new AgDataSource { Id = "form-1", Type = AgDataSourceType.Form },
            Dimensions = dimensions,
            Metrics = metrics,
        };
    }
}
