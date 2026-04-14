using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class ExportLogRouteResolverTests
    {
        [TestMethod]
        public void ResolveQueueName_ReturnsExportLogQueueName()
        {
            var resolver = new AttributeMessageRouteResolver();
            var queueName = resolver.ResolveQueueName(typeof(ExportLogTaskArgs));

            Assert.AreEqual("export-log", queueName);
        }
    }
}
