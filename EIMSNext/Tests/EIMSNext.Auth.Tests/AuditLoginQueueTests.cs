using System.Collections.Concurrent;
using EIMSNext.Auth.Entities;
using EIMSNext.Auth.Interfaces;
using EIMSNext.Auth.Models;
using EIMSNext.Auth.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EIMSNext.Auth.Tests;

[TestClass]
public class AuditLoginQueueTests
{
    [TestMethod]
    public void Options_UseDefaultsWhenConfigurationSectionIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddOptions<AuditLoginQueueOptions>()
            .Bind(configuration.GetSection(AuditLoginQueueOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AuditLoginQueueOptions>>().Value;

        Assert.AreEqual(AuditLoginQueueOptions.DefaultCapacity, options.Capacity);
        Assert.AreEqual(AuditLoginQueueOptions.DefaultBatchSize, options.BatchSize);
        Assert.AreEqual(AuditLoginQueueOptions.DefaultFlushIntervalMs, options.FlushIntervalMs);
        Assert.AreEqual(AuditLoginQueueOptions.DefaultShutdownDrainSeconds, options.ShutdownDrainSeconds);
    }

    [TestMethod]
    public async Task AddAuditLogin_QueuesWithoutWritingSynchronously()
    {
        using var dbContext = new RecordingAuthDbContext();
        var queue = CreateQueue(capacity: 1);
        var service = new AuditLoginService(
            dbContext,
            queue,
            NullLogger<AuditLoginService>.Instance);
        var auditLogin = new AuditLogin { LoginId = "user@example.com" };

        await service.AddAuditLogin(auditLogin);

        Assert.IsFalse(string.IsNullOrWhiteSpace(auditLogin.Id));
        Assert.AreEqual(1, queue.PendingCount);
        Assert.AreEqual(0, dbContext.DirectWrites.Count);
    }

    [TestMethod]
    public async Task AddAuditLogin_WritesSynchronouslyWhenQueueIsFull()
    {
        using var dbContext = new RecordingAuthDbContext();
        var queue = CreateQueue(capacity: 1);
        Assert.IsTrue(queue.TryEnqueue(new AuditLogin { Id = "queued" }));
        var service = new AuditLoginService(
            dbContext,
            queue,
            NullLogger<AuditLoginService>.Instance);
        var overflowAudit = new AuditLogin { Id = "overflow" };

        await service.AddAuditLogin(overflowAudit);

        Assert.AreEqual(1, queue.PendingCount);
        Assert.AreEqual("overflow", dbContext.DirectWrites.Single().Id);
    }

    [TestMethod]
    public async Task WriterService_PersistsQueuedAuditsAndUpdatesPendingCount()
    {
        var options = Options.Create(new AuditLoginQueueOptions
        {
            Capacity = 10,
            BatchSize = 10,
            FlushIntervalMs = 10,
            ShutdownDrainSeconds = 2
        });
        var queue = new AuditLoginQueue(options);
        using var dbContext = new RecordingAuthDbContext();
        using var writer = new AuditLoginWriterService(
            queue,
            dbContext,
            options,
            NullLogger<AuditLoginWriterService>.Instance);

        await writer.StartAsync(CancellationToken.None);
        try
        {
            Assert.IsTrue(queue.TryEnqueue(new AuditLogin { Id = "audit-1" }));
            Assert.IsTrue(queue.TryEnqueue(new AuditLogin { Id = "audit-2" }));

            await WaitUntilAsync(
                () => dbContext.BatchWrites.Count == 2,
                TimeSpan.FromSeconds(2));

            Assert.AreEqual(0, queue.PendingCount);
            CollectionAssert.AreEquivalent(
                new[] { "audit-1", "audit-2" },
                dbContext.BatchWrites.Select(x => x.Id).ToArray());
            Assert.IsTrue(dbContext.BatchWriteCalls > 0);
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task WriterService_DrainsQueuedAuditsWhenStoppedBeforeNextFlush()
    {
        var options = Options.Create(new AuditLoginQueueOptions
        {
            Capacity = 10,
            BatchSize = 10,
            FlushIntervalMs = 60_000,
            ShutdownDrainSeconds = 2
        });
        var queue = new AuditLoginQueue(options);
        using var dbContext = new RecordingAuthDbContext();
        using var writer = new AuditLoginWriterService(
            queue,
            dbContext,
            options,
            NullLogger<AuditLoginWriterService>.Instance);

        await writer.StartAsync(CancellationToken.None);
        Assert.IsTrue(queue.TryEnqueue(new AuditLogin { Id = "shutdown-audit" }));

        await writer.StopAsync(CancellationToken.None);

        Assert.AreEqual(0, queue.PendingCount);
        Assert.AreEqual("shutdown-audit", dbContext.BatchWrites.Single().Id);
    }

    private static AuditLoginQueue CreateQueue(int capacity)
    {
        return new AuditLoginQueue(Options.Create(new AuditLoginQueueOptions
        {
            Capacity = capacity
        }));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail("Timed out waiting for the audit login writer.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class RecordingAuthDbContext : IAuthDbContext
    {
        private readonly ConcurrentQueue<AuditLogin> _directWrites = new();
        private readonly ConcurrentQueue<AuditLogin> _batchWrites = new();
        private int _batchWriteCalls;

        public IReadOnlyCollection<AuditLogin> DirectWrites => _directWrites.ToArray();
        public IReadOnlyCollection<AuditLogin> BatchWrites => _batchWrites.ToArray();
        public int BatchWriteCalls => Volatile.Read(ref _batchWriteCalls);

        public IQueryable<Client> Clients => Array.Empty<Client>().AsQueryable();
        public IQueryable<User> Users => Array.Empty<User>().AsQueryable();
        public IQueryable<EmployeeLookup> Employees => Array.Empty<EmployeeLookup>().AsQueryable();
        public IQueryable<PublicAccessSetting> PublicSettings => Array.Empty<PublicAccessSetting>().AsQueryable();
        public IQueryable<CorporateSettingReadModel> CorporateSettings => Array.Empty<CorporateSettingReadModel>().AsQueryable();

        public Task AddClient(Client entity) => Task.CompletedTask;
        public Task UpdateClient(Client entity) => Task.CompletedTask;
        public Task AddUser(User entity) => Task.CompletedTask;
        public Task UpdateUser(User entity) => Task.CompletedTask;

        public Task AddAuditLogin(AuditLogin entity)
        {
            _directWrites.Enqueue(entity);
            return Task.CompletedTask;
        }

        public Task AddAuditLogins(
            IReadOnlyCollection<AuditLogin> entities,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _batchWriteCalls);
            foreach (var entity in entities)
            {
                _batchWrites.Enqueue(entity);
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
