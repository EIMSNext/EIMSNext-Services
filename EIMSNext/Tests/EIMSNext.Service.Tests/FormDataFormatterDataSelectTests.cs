using System.Dynamic;

using EIMSNext.Common;
using EIMSNext.Component;
using EIMSNext.Service.Entities;

namespace EIMSNext.Service.Tests
{
    [TestClass]
    public class FormDataFormatterDataSelectTests
    {
        [TestMethod]
        public void FormatForDisplay_DataSelect_IncludesLabelsAndValues()
        {
            dynamic data = new ExpandoObject();
            data.sourceSelection = new[]
            {
                new Dictionary<string, object?> { ["label"] = "商品名称", ["value"] = "测试商品" },
                new Dictionary<string, object?> { ["label"] = "数量", ["value"] = "35" },
            };

            var formData = new FormData { Data = data };
            var fieldDefs = new List<FieldDef>
            {
                new() { Field = "sourceSelection", Title = "库存数据选择", Type = FieldType.DataSelect },
            };

            var result = (IDictionary<string, object?>)FormDataFormatter.FormatForDisplay(formData, fieldDefs);

            Assert.AreEqual("商品名称: 测试商品; 数量: 35", result["sourceSelection"]);
        }
    }
}
