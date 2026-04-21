using EIMSNext.Async.Tasks.Export;
using EIMSNext.Core.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class ExportLogSeekPaginationTests
    {
        private sealed class TestEntity : EntityBase
        {
        }

        [TestMethod]
        public void BuildSeekFilter_ShouldReturnBaseFilter_WhenCursorIsMissing()
        {
            var builder = Builders<TestEntity>.Filter;
            var baseFilter = builder.Eq(x => x.DeleteFlag, false);

            var filter = ExportProcessorBase.BuildSeekFilter(baseFilter, builder, null, null);
            var rendered = Render(filter);

            StringAssert.Contains(rendered, "DeleteFlag");
            Assert.IsFalse(rendered.Contains("createTime", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(rendered.Contains("_id", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void BuildSeekFilter_ShouldUseCreateTimeAndIdAsSeekCursor()
        {
            var builder = Builders<TestEntity>.Filter;
            var baseFilter = builder.Eq(x => x.DeleteFlag, false);

            var filter = ExportProcessorBase.BuildSeekFilter(baseFilter, builder, 1000L, "id-002");
            var rendered = Render(filter);

            StringAssert.Contains(rendered, "DeleteFlag");
            StringAssert.Contains(rendered, "CreateTime");
            StringAssert.Contains(rendered, "_id");
            StringAssert.Contains(rendered, "\"$lt\" : 1000");
            StringAssert.Contains(rendered, "\"CreateTime\" : 1000");
            StringAssert.Contains(rendered, "id-002");
            StringAssert.Contains(rendered, "\"$or\"");
        }
        
        private static string Render<T>(FilterDefinition<T> filter)
        {
            var serializer = BsonSerializer.LookupSerializer<T>();
            var registry = BsonSerializer.SerializerRegistry;
            return filter.Render(new RenderArgs<T>(serializer, registry)).ToJson();
        }
    }
}
