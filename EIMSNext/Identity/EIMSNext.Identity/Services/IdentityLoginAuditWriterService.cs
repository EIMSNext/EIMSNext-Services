using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EIMSNext.Identity.Services;

public sealed class IdentityLoginAuditWriterService : BackgroundService
{
    private readonly IdentityLoginAuditQueue _queue;
    private readonly IIdentityDbContext _dbContext;
    private readonly ILogger<IdentityLoginAuditWriterService> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly TimeSpan _shutdownDrainTimeout;
    private IReadOnlyCollection<IdentityLoginAudit>? _inFlightBatch;

    public IdentityLoginAuditWriterService(
        IdentityLoginAuditQueue queue,
        IIdentityDbContext dbContext,
        IOptions<IdentityLoginAuditQueueOptions> options,
        ILogger<IdentityLoginAuditWriterService> logger)
    {
        _queue = queue;
        _dbContext = dbContext;
        _logger = logger;
        _batchSize = Math.Max(1, options.Value.BatchSize);
        _flushInterval = TimeSpan.FromMilliseconds(Math.Max(10, options.Value.FlushIntervalMs));
        _shutdownDrainTimeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.ShutdownDrainSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_flushInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAvailableAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // StopAsync completes the channel and the final drain handles remaining records.
        }
        finally
        {
            using var drainCts = new CancellationTokenSource(_shutdownDrainTimeout);
            try
            {
                await DrainAsync(drainCts.Token);
            }
            catch (OperationCanceledException) when (drainCts.IsCancellationRequested)
            {
                _logger.LogCritical(
                    "Timed out draining audit login queue; {PendingCount} records remain in memory",
                    _queue.PendingCount);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        await base.StopAsync(cancellationToken);

        // ExecuteAsync normally drains in its finally block. This second pass covers a
        // shutdown that races before the background loop observes its first timer tick.
        if (_queue.PendingCount > 0)
        {
            using var drainCts = new CancellationTokenSource(_shutdownDrainTimeout);
            await DrainAsync(drainCts.Token);
        }
    }

    private async Task FlushAvailableAsync(CancellationToken cancellationToken)
    {
        while (_queue.Reader.TryPeek(out _))
        {
            var batch = ReadBatch();
            if (batch.Count == 0)
            {
                return;
            }

            await PersistWithRetryAsync(batch, cancellationToken);
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        if (_inFlightBatch is { Count: > 0 })
        {
            await PersistWithRetryAsync(_inFlightBatch, cancellationToken);
        }

        while (_queue.Reader.TryPeek(out _) || await _queue.Reader.WaitToReadAsync(cancellationToken))
        {
            var batch = ReadBatch();
            if (batch.Count > 0)
            {
                await PersistWithRetryAsync(batch, cancellationToken);
            }
        }
    }

    private List<IdentityLoginAudit> ReadBatch()
    {
        var batch = new List<IdentityLoginAudit>(_batchSize);
        while (batch.Count < _batchSize && _queue.Reader.TryRead(out var auditLogin))
        {
            batch.Add(auditLogin);
        }

        return batch;
    }

    private async Task PersistWithRetryAsync(
        IReadOnlyCollection<IdentityLoginAudit> batch,
        CancellationToken cancellationToken)
    {
        _inFlightBatch = batch;
        var retryDelay = TimeSpan.FromMilliseconds(100);
        while (true)
        {
            try
            {
                await _dbContext.AddIdentityLoginAudits(batch, cancellationToken);
                _queue.MarkPersisted(batch.Count);
                _inFlightBatch = null;
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist {AuditCount} audit login records; retrying in {RetryDelayMs} ms",
                    batch.Count,
                    retryDelay.TotalMilliseconds);
                await Task.Delay(retryDelay, cancellationToken);
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2, 5_000));
            }
        }
    }
}
