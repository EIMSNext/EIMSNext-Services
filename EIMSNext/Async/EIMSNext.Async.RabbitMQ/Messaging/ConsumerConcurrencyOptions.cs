namespace EIMSNext.Async.RabbitMQ.Messaging
{
    public class ConsumerConcurrencyOptions
    {
        public Dictionary<string, QueueConcurrencyOptions> Queues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public QueueConcurrencyOptions GetQueueOptions(string queueName)
        {
            if (Queues.TryGetValue(queueName, out var options))
            {
                return options.Normalize();
            }

            return QueueConcurrencyOptions.Default;
        }
    }

    public class QueueConcurrencyOptions
    {
        public static QueueConcurrencyOptions Default => new();

        public int Concurrency { get; set; } = 1;

        public ushort PrefetchCount { get; set; } = 1;

        public QueueConcurrencyOptions Normalize()
        {
            if (Concurrency < 1)
            {
                Concurrency = 1;
            }

            if (PrefetchCount < 1)
            {
                PrefetchCount = 1;
            }

            return this;
        }
    }
}
