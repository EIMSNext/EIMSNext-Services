using EIMSNext.Core.Mongo.Entities;

namespace EIMSNext.Service.Persistence.Outbox
{
    public sealed class OutboxMessage : MongoEntityBase
    {
        public string QueueName { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
        public long OutAt { get; set; }
        public int Attempt { get; set; }
        public long? LastAttemptTime { get; set; }
        public string? Error { get; set; }
        public long? SentTime { get; set; }
        public DateTime? SentAt { get; set; }
    }

    public enum OutboxStatus
    {
        Pending,
        Sent,
        Failed
    }

    public sealed class ProcessedMessage : MongoEntityBase
    {
        public string EventKey { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Status { get; set; } = ProcessedMessageStatus.Processing;
        public DateTime? LeaseUntil { get; set; }
        public string? LeaseToken { get; set; }
        public long? ProcessedTime { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public static class ProcessedMessageStatus
    {
        public const string Processing = "Processing";
        public const string Completed = "Completed";
    }
}
