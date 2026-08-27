using System.Collections.Concurrent;
using EIMSNext.Entities;
using EIMSNext.Identity.Interfaces;
using EIMSNext.Identity.Models;
using EIMSNext.Identity.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EIMSNext.Identity.Tests;

[TestClass]
public class IdentityLoginAuditQueueTests
{
    [TestMethod]
    public void Options_UseDefaultsWhenConfigurationSectionIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddOptions<IdentityLoginAuditQueueOptions>()
            .Bind(configuration.GetSection(IdentityLoginAuditQueueOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<IdentityLoginAuditQueueOptions>>().Value;

        Assert.AreEqual(IdentityLoginAuditQueueOptions.DefaultCapacity, options.Capacity);
        Assert.AreEqual(IdentityLoginAuditQueueOptions.DefaultBatchSize, options.BatchSize);
        Assert.AreEqual(IdentityLoginAuditQueueOptions.DefaultFlushIntervalMs, options.FlushIntervalMs);
        Assert.AreEqual(IdentityLoginAuditQueueOptions.DefaultShutdownDrainSeconds, options.ShutdownDrainSeconds);
    }

    [TestMethod]
    public async Task AddIdentityLoginAudit_QueuesWithoutWritingSynchronously()
    {
        using var dbContext = new RecordingIdentityDbContext();
        var queue = CreateQueue(capacity: 1);
        var service = new IdentityLoginAuditService(
            dbContext,
            queue,
            NullLogger<IdentityLoginAuditService>.Instance);
        var auditLogin = new IdentityLoginAudit { LoginId = "user@example.com" };

        await service.AddIdentityLoginAudit(auditLogin);

        Assert.IsFalse(string.IsNullOrWhiteSpace(auditLogin.Id));
        Assert.AreEqual(1, queue.PendingCount);
        Assert.AreEqual(0, dbContext.DirectWrites.Count);
    }

    [TestMethod]
    public async Task AddIdentityLoginAudit_WritesSynchronouslyWhenQueueIsFull()
    {
        using var dbContext = new RecordingIdentityDbContext();
        var queue = CreateQueue(capacity: 1);
        Assert.IsTrue(queue.TryEnqueue(new IdentityLoginAudit { Id = "queued" }));
        var service = new IdentityLoginAuditService(
            dbContext,
            queue,
            NullLogger<IdentityLoginAuditService>.Instance);
        var overflowAudit = new IdentityLoginAudit { Id = "overflow" };

        await service.AddIdentityLoginAudit(overflowAudit);

        Assert.AreEqual(1, queue.PendingCount);
        Assert.AreEqual("overflow", dbContext.DirectWrites.Single().Id);
    }

    [TestMethod]
    public async Task WriterService_PersistsQueuedAuditsAndUpdatesPendingCount()
    {
        var options = Options.Create(new IdentityLoginAuditQueueOptions
        {
            Capacity = 10,
            BatchSize = 10,
            FlushIntervalMs = 10,
            ShutdownDrainSeconds = 2
        });
        var queue = new IdentityLoginAuditQueue(options);
        using var dbContext = new RecordingIdentityDbContext();
        using var writer = new IdentityLoginAuditWriterService(
            queue,
            dbContext,
            options,
            NullLogger<IdentityLoginAuditWriterService>.Instance);

        await writer.StartAsync(CancellationToken.None);
        try
        {
            Assert.IsTrue(queue.TryEnqueue(new IdentityLoginAudit { Id = "audit-1" }));
            Assert.IsTrue(queue.TryEnqueue(new IdentityLoginAudit { Id = "audit-2" }));

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
        var options = Options.Create(new IdentityLoginAuditQueueOptions
        {
            Capacity = 10,
            BatchSize = 10,
            FlushIntervalMs = 60_000,
            ShutdownDrainSeconds = 2
        });
        var queue = new IdentityLoginAuditQueue(options);
        using var dbContext = new RecordingIdentityDbContext();
        using var writer = new IdentityLoginAuditWriterService(
            queue,
            dbContext,
            options,
            NullLogger<IdentityLoginAuditWriterService>.Instance);

        await writer.StartAsync(CancellationToken.None);
        Assert.IsTrue(queue.TryEnqueue(new IdentityLoginAudit { Id = "shutdown-audit" }));

        await writer.StopAsync(CancellationToken.None);

        Assert.AreEqual(0, queue.PendingCount);
        Assert.AreEqual("shutdown-audit", dbContext.BatchWrites.Single().Id);
    }

    private static IdentityLoginAuditQueue CreateQueue(int capacity)
    {
        return new IdentityLoginAuditQueue(Options.Create(new IdentityLoginAuditQueueOptions
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

    private sealed class RecordingIdentityDbContext : IIdentityDbContext
    {
        private readonly ConcurrentQueue<IdentityLoginAudit> _directWrites = new();
        private readonly ConcurrentQueue<IdentityLoginAudit> _batchWrites = new();
        private int _batchWriteCalls;

        public IReadOnlyCollection<IdentityLoginAudit> DirectWrites => _directWrites.ToArray();
        public IReadOnlyCollection<IdentityLoginAudit> BatchWrites => _batchWrites.ToArray();
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

        public Task AddIdentityLoginAudit(IdentityLoginAudit entity)
        {
            _directWrites.Enqueue(entity);
            return Task.CompletedTask;
        }

        public Task AddIdentityLoginAudits(
            IReadOnlyCollection<IdentityLoginAudit> entities,
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
