using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class DataExportRouteResolverTests
    {
        [TestMethod]
        public void ResolveQueueName_ReturnsDataExportQueueName()
        {
            var resolver = new AttributeMessageRouteResolver();
            var queueName = resolver.ResolveQueueName(typeof(DataExportTaskArgs));

            Assert.AreEqual("data-export", queueName);
        }
    }
}
