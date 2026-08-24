namespace EIMSNext.Async.Abstractions.Messaging
{
    public interface IOutboxMessage
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class OutboxQueueAttribute : Attribute
    {
        public OutboxQueueAttribute(string queueName)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException("Queue name cannot be empty.", nameof(queueName));
            }

            QueueName = queueName;
        }

        public string QueueName { get; }
    }
}
