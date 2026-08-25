using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EIMSNext.Auth.Services;

public sealed class AuditLoginWriterService : BackgroundService
{
    private readonly AuditLoginQueue _queue;
    private readonly IAuthDbContext _dbContext;
    private readonly ILogger<AuditLoginWriterService> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly TimeSpan _shutdownDrainTimeout;
    private IReadOnlyCollection<AuditLogin>? _inFlightBatch;

    public AuditLoginWriterService(
        AuditLoginQueue queue,
        IAuthDbContext dbContext,
        IOptions<AuditLoginQueueOptions> options,
        ILogger<AuditLoginWriterService> logger)
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

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        return base.StopAsync(cancellationToken);
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

    private List<AuditLogin> ReadBatch()
    {
        var batch = new List<AuditLogin>(_batchSize);
        while (batch.Count < _batchSize && _queue.Reader.TryRead(out var auditLogin))
        {
            batch.Add(auditLogin);
        }

        return batch;
    }

    private async Task PersistWithRetryAsync(
        IReadOnlyCollection<AuditLogin> batch,
        CancellationToken cancellationToken)
    {
        _inFlightBatch = batch;
        var retryDelay = TimeSpan.FromMilliseconds(100);
        while (true)
        {
            try
            {
                await _dbContext.AddAuditLogins(batch, cancellationToken);
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
