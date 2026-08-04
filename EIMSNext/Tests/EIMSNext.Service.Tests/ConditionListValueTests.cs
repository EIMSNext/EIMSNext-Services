using System.Text.Json;

using EIMSNext.Component;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class ConditionListValueTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        [TestMethod]
        public void ToDynamicFilter_CustomString_UsesClrStringValue()
        {
            var condition = JsonSerializer.Deserialize<ConditionList>("""
                {
                  "rel": "and",
                  "items": [
                    {
                      "field": { "formId": "form-1", "field": "status", "type": "radio" },
                      "op": "eq",
                      "value": { "type": "custom", "value": "yes" }
                    }
                  ]
                }
                """, JsonOptions)!;

            var filter = condition.ToDynamicFilter().Items![0];

            Assert.IsInstanceOfType<string>(filter.Value);
            Assert.AreEqual("yes", filter.Value);
        }

        [TestMethod]
        public void ToDynamicFilter_CustomNumber_UsesClrNumberValue()
        {
            var condition = JsonSerializer.Deserialize<ConditionList>("""
                {
                  "field": { "formId": "form-1", "field": "amount", "type": "number" },
                  "op": "eq",
                  "value": { "type": "custom", "value": 41 }
                }
                """, JsonOptions)!;

            var filter = condition.ToDynamicFilter();

            Assert.IsTrue(filter.Value is long or decimal);
            Assert.AreEqual(41L, Convert.ToInt64(filter.Value));
        }

        [TestMethod]
        public void ToScriptExpression_CustomString_DoesNotContainJsonElementFormatting()
        {
            var condition = JsonSerializer.Deserialize<ConditionList>("""
                {
                  "field": { "formId": "form-1", "field": "status", "type": "radio", "nodeId": "start" },
                  "op": "eq",
                  "value": { "type": "custom", "value": "yes" }
                }
                """, JsonOptions)!;

            var expression = condition.ToScriptExpression();

            StringAssert.Contains(expression, "'yes'");
            Assert.IsFalse(expression.Contains("JsonElement", StringComparison.Ordinal));
        }
    }
}
