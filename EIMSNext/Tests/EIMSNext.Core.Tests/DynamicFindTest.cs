using System.Text.Json;
using EIMSNext.Common;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Tests
{
    [TestClass]
    public class DynamicFindTest : TestBase
    {
        [TestMethod]
        public void DeserializeTest()
        {
            var jsonFilter = "{\"filter\":{\"rel\":\"And\",\"field\":\"_id\",\"type\":\"None\",\"op\":\"Eq\",\"value\":[\"67de5e1ace67843829f57205\"]},\"skip\":0,\"take\":20}";
            var opt = jsonFilter.DeserializeFromJson<DynamicFindOptions<FormData>>();

            Assert.IsNotNull(opt);
            Assert.IsNotNull(opt.Filter);
            var predicate = opt.Filter.ToPredicate<FormData>();
 
            jsonFilter = "{\"filter\":{\"rel\":\"Or\",\"items\":[{\"rel\":\"And\",\"field\":\"_id\",\"type\":\"None\",\"op\":\"Eq\",\"value\":[\"67de5e1ace67843829f57205\"]},{\"rel\":\"And\",\"field\":\"code\",\"type\":\"None\",\"op\":\"In\",\"value\":[1,2]}],\"type\":\"None\",\"op\":\"Eq\"},\"skip\":0,\"take\":20}";
            opt = jsonFilter.DeserializeFromJson<DynamicFindOptions<FormData>>();

            Assert.IsNotNull(opt);
            Assert.IsNotNull(opt.Filter);
            Assert.IsNotNull(opt.Filter.Items);
        }

        [TestMethod]
        public void DeserializeDynamicFindOptions()
        {
            var jsonFilter = "{\"filter\":{\"rel\": \"And\",\"items\": [{\"field\": \"formId\",\"type\": \"none\",\"op\": \"Eq\",\"value\": \"68298220d23e843cb3001645\"}]},\"skip\":0,\"take\":20}";
            var opt = jsonFilter.DeserializeFromJson<DynamicFindOptions<FormData>>();
            Assert.IsNotNull(opt);
            Assert.IsNotNull(opt.Filter);
        }

        [TestMethod]
        public void CountDocuments()
        {
            var jsonFilter = "{\"filter\":{\"rel\": \"And\",\"items\": [{\"field\": \"formId\",\"type\": \"none\",\"op\": \"Eq\",\"value\": \"68298220d23e843cb3001645\"}]},\"skip\":0,\"take\":20}";
            var opt = jsonFilter.DeserializeFromJson<DynamicFindOptions<FormData>>();

            var resp = new FormDataRepository(_dbContext!);
            var data = new FormData { FormId = "68298220d23e843cb3001645" };
            resp.Insert(data);
            // EF Core 侧对 IQueryable 直接用 Count()。
            var result = resp.Find(opt!).Count();

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void DollarPrefixedStringValue_IsAllowed()
        {
            var filter = new DynamicFilter
            {
                Field = "productName",
                Op = FilterOp.Eq,
                Value = "$100"
            };

            Assert.IsNotNull(filter.ToPredicate<FormData>());
        }

        [TestMethod]
        public void MongoOperatorObject_IsRejectedAfterJsonNormalization()
        {
            var jsonFilter = "{\"field\":\"productName\",\"op\":\"eq\",\"value\":{\"$ne\":\"never\"}}";
            var filter = jsonFilter.DeserializeFromJson<DynamicFilter>();

            Assert.IsNotNull(filter);
            try
            {
                filter.ToPredicate<FormData>();
                Assert.Fail("应拒绝 Mongo 操作符对象");
            }
            catch (BadRequestException error)
            {
                Assert.AreEqual("过滤值不允许包含操作符对象", error.Message);
            }
        }
    }
}
