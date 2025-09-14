using System.Text.Json;
using EIMSNext.Common.Extension;
using EIMSNext.Core.Query;

namespace EIMSNext.Core.Test
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
            var mgFilter = opt.Filter.ToFilterDefinition<FormData>();
 
            jsonFilter = "{\"filter\":{\"rel\":\"Or\",\"items\":[{\"rel\":\"And\",\"field\":\"_id\",\"type\":\"None\",\"op\":\"Eq\",\"value\":[\"67de5e1ace67843829f57205\"]},{\"rel\":\"And\",\"field\":\"code\",\"type\":\"None\",\"op\":\"In\",\"value\":[1,2]}],\"type\":\"None\",\"op\":\"Eq\"},\"skip\":0,\"take\":20}";
            opt = jsonFilter.DeserializeFromJson<DynamicFindOptions<FormData>>();

            Assert.IsNotNull(opt);
            Assert.IsNotNull(opt.Filter);
            Assert.IsNotNull(opt.Filter.Items);
        }       
    }
}
