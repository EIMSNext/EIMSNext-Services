namespace EIMSNext.Async.Abstractions.Messaging
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class QueueAttribute(string queueName) : Attribute
    {
        public string QueueName { get; } = queueName;
    }
}
