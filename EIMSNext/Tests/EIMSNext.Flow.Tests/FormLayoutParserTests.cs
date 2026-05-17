using EIMSNext.Component;

namespace EIMSNext.Flow.Tests
{
    [TestClass]
    public class FormLayoutParserTests
    {
        [TestMethod]
        public void Parse_Should_ResolveDepends_FromCurrentFieldSet()
        {
            var parser = new FormLayoutParser();
            var layout = """
            [
              {
                "type": "input",
                "field": "f_abc12345",
                "title": "Field A"
              },
              {
                "type": "number",
                "field": "custom_qty",
                "title": "Qty"
              },
              {
                "type": "tableform",
                "field": "detail",
                "title": "Detail",
                "props": {
                  "columns": [
                    {
                      "rule": [
                        {
                          "type": "number",
                          "field": "detail_amount",
                          "title": "Amount"
                        }
                      ]
                    },
                    {
                      "rule": [
                        {
                          "type": "number",
                          "field": "detail_qty",
                          "title": "Qty"
                        }
                      ]
                    }
                  ]
                }
              },
              {
                "type": "input",
                "field": "custom_total",
                "title": "Total",
                "computed": {
                  "value": "SUM(f_abc12345, custom_qty, COLUMN(\"detail_amount\"), detail.detail_qty)"
                }
              }
            ]
            """;

            var fields = parser.Parse(layout);
            var totalField = fields.Single(x => x.Field == "custom_total");

            Assert.AreEqual("detail_amount,detail>detail_qty,f_abc12345,custom_qty", totalField.Props.ValueProp?.Depends);
        }

        [TestMethod]
        public void Parse_Should_KeepLegacyFieldNames_WhenMatched()
        {
            var parser = new FormLayoutParser();
            var layout = """
            [
              {
                "type": "input",
                "field": "jlegacyfield001",
                "title": "Legacy"
              },
              {
                "type": "input",
                "field": "manual_field",
                "title": "Manual",
                "computed": {
                  "value": "CONCAT(jlegacyfield001, '-', manual_field)"
                }
              }
            ]
            """;

            var fields = parser.Parse(layout);
            var manualField = fields.Single(x => x.Field == "manual_field");

            Assert.AreEqual("jlegacyfield001,manual_field", manualField.Props.ValueProp?.Depends);
        }
    }
}
