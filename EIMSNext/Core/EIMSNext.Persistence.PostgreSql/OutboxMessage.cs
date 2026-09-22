using EIMSNext.Core.Entities;

namespace EIMSNext.Persistence.PostgreSql.Outbox;

/// <summary>
/// 发件箱消息。Id 为字符串契约（不使用数据库自增），因此仍继承
/// <see cref="KeyedEntityBase"/> 以获得统一的 Id 定义；该基类只提供 Id 属性，
/// 不引入任何 MongoDB 依赖。
/// </summary>
public sealed class OutboxMessage : KeyedEntityBase
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

public enum OutboxStatus { Pending, Sent, Failed }

/// <summary>
/// 幂等去重表，记录已处理的消费事件，防止重复投递导致业务重复执行。
/// </summary>
public sealed class ProcessedMessage : KeyedEntityBase
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
