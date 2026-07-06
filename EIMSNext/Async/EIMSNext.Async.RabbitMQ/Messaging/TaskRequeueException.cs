namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public sealed class TaskRequeueException : Exception
    {
        public TaskRequeueException(string message, TimeSpan? delay = null)
            : base(message)
        {
            Delay = delay ?? TimeSpan.Zero;
        }

        public TimeSpan Delay { get; }
    }
}
