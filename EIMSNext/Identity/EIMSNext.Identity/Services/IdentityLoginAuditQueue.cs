using System.Threading.Channels;
using EIMSNext.Entities;
using Microsoft.Extensions.Options;

namespace EIMSNext.Identity.Services;

public sealed class IdentityLoginAuditQueueOptions
{
    public const string SectionName = "IdentityLoginAuditQueue";
    public const int DefaultCapacity = 10_000;
    public const int DefaultBatchSize = 100;
    public const int DefaultFlushIntervalMs = 100;
    public const int DefaultShutdownDrainSeconds = 10;

    public int Capacity { get; set; } = DefaultCapacity;
    public int BatchSize { get; set; } = DefaultBatchSize;
    public int FlushIntervalMs { get; set; } = DefaultFlushIntervalMs;
    public int ShutdownDrainSeconds { get; set; } = DefaultShutdownDrainSeconds;
}

public sealed class IdentityLoginAuditQueue
{
    private readonly Channel<IdentityLoginAudit> _channel;
    private int _pendingCount;

    public IdentityLoginAuditQueue(IOptions<IdentityLoginAuditQueueOptions> options)
    {
        var capacity = Math.Max(1, options.Value.Capacity);
        _channel = Channel.CreateBounded<IdentityLoginAudit>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    internal ChannelReader<IdentityLoginAudit> Reader => _channel.Reader;

    public int PendingCount => Volatile.Read(ref _pendingCount);

    public bool TryEnqueue(IdentityLoginAudit auditLogin)
    {
        Interlocked.Increment(ref _pendingCount);
        if (!_channel.Writer.TryWrite(auditLogin))
        {
            Interlocked.Decrement(ref _pendingCount);
            return false;
        }

        return true;
    }

    internal void MarkPersisted(int count)
    {
        Interlocked.Add(ref _pendingCount, -count);
    }

    internal void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
