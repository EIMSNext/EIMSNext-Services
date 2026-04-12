using EIMSNext.Async.Abstractions.Messaging;
using EIMSNext.Async.RabbitMQ.Messaging;

namespace EIMSNext.Async.Tests
{
    [TestClass]
    public class AttributeMessageRouteResolverTests
    {
        private readonly AttributeMessageRouteResolver _resolver = new();

        [TestMethod]
        public void ResolveQueueName_ReturnsQueueFromAttribute()
        {
            var queueName = _resolver.ResolveQueueName(typeof(NotifyDispatchTaskArgs));

            Assert.AreEqual("notify-dispatch", queueName);
        }

        [TestMethod]
        public void ResolveQueueName_ThrowsWhenAttributeMissing()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => _resolver.ResolveQueueName(typeof(NoQueueMessage)));
        }

        private sealed class NoQueueMessage
        {
        }
    }
}
