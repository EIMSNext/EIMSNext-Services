using System.Text.Json;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Test
{
    [TestClass]
    public class FormDataFindTest : TestBase
    {
        [TestMethod]
        public void FindTest()
        {
            var jsonFilter = "{\"filter\":{\"rel\": \"and\",\"items\": [{\"field\": \"formId\",\"type\": \"none\",\"op\": \"eq\",\"value\": \"68298220d23e843cb3001645\"}]},\"skip\":0,\"take\":20}";
            var opt = jsonFilter.DeserializeFromJson<DynamicFindOptions<FormData>>();

            var resp = new FormDataRepository(_dbContext!);
            var result = resp.Find(opt!).CountDocuments();

            Assert.IsTrue(result > 0);
        }
    }
}
