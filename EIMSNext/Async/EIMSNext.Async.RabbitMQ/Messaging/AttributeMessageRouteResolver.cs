using EIMSNext.Async.Abstractions.Messaging;

namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public class AttributeMessageRouteResolver : IMessageRouteResolver
    {
        public string ResolveQueueName(Type messageType)
        {
            var attribute = messageType.GetCustomAttributes(typeof(QueueAttribute), inherit: false)
                .OfType<QueueAttribute>()
                .FirstOrDefault();

            if (attribute == null || string.IsNullOrWhiteSpace(attribute.QueueName))
            {
                throw new InvalidOperationException($"Message type '{messageType.FullName}' is missing QueueAttribute.");
            }

            return attribute.QueueName.ToLowerInvariant();
        }
    }
}
