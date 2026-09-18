using System.Dynamic;
using EIMSNext.Flow.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using WorkflowCore.Models;

namespace EIMSNext.Flow.Tests;

[TestClass]
public sealed class PostgreSqlPersistenceTests
{
    [TestMethod]
    public async Task RuntimeStore_PersistsState_ClaimsOnce_RetriesCommands_AndPurges()
    {
        var connectionString = Environment.GetEnvironmentVariable("EIMS_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive("Set EIMS_TEST_POSTGRES to a PostgreSQL test connection.");
            return;
        }

        // An isolated schema keeps tests away from application data, even on a
        // shared local server. The schema name is generated here, never supplied.
        var schema = "wf_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false };
            await using (var setup = new NpgsqlConnection(settings.ConnectionString))
            {
                await setup.OpenAsync();
                var root = FindSolutionRoot();
                foreach (var name in new[] { "004_CreateWorkflowTables.sql", "005_CreateWorkflowIndexes.sql" })
                {
                    var sql = await File.ReadAllTextAsync(Path.Combine(root, "ApiHost", "EIMSNext.Tool.DbMaintenance", "Sql", name));
                    await using var command = new NpgsqlCommand(sql, setup);
                    await command.ExecuteNonQueryAsync();
                }
            }

            var options = new DbContextOptionsBuilder<TestContext>()
                .UseNpgsql(settings.ConnectionString, pg => pg.EnableRetryOnFailure(1)).Options;
            Task<IWfDbContext> Create(CancellationToken cancellationToken) => Task.FromResult<IWfDbContext>(new TestContext(options));
            var store = new PostgreSqlPersistenceProvider(Create, NullLogger<PostgreSqlPersistenceProvider>.Instance);
            var now = DateTime.UtcNow;
            dynamic data = new ExpandoObject();
            data.CorpId = "123";
            data.Round = 2;
            data.Items = new object[] { "first", 9007199254740993L };
            var workflow = new WorkflowInstance
            {
                Id = "wf-one", WorkflowDefinitionId = "definition", Version = 1,
                Reference = "form-row", Status = WorkflowStatus.Runnable, CreateTime = now,
                NextExecution = now.AddSeconds(-1).Ticks, Data = data,
                ExecutionPointers = new ExecutionPointerCollection
                {
                    new ExecutionPointer { Id = "pointer", StepId = 3, Active = true, PersistenceData = data },
                    new ExecutionPointer
                    {
                        Id = "control", StepId = 4, Active = true,
                        PersistenceData = new ControlPersistenceData { ChildrenActive = true }
                    }
                }
            };
            Assert.AreEqual("wf-one", await store.CreateNewWorkflow(workflow));
            var loaded = await store.GetWorkflowInstance("wf-one");
            Assert.IsInstanceOfType<ExpandoObject>(loaded.Data);
            var loadedData = (IDictionary<string, object>)loaded.Data;
            Assert.AreEqual(2L, loadedData["Round"]);
            Assert.AreEqual(9007199254740993L, ((object[])loadedData["Items"])[1]);
            Assert.IsInstanceOfType<ExpandoObject>(loaded.ExecutionPointers.Single(x => x.Id == "pointer").PersistenceData);
            var controlState = loaded.ExecutionPointers.Single(x => x.Id == "control").PersistenceData;
            Assert.IsInstanceOfType<ControlPersistenceData>(controlState);
            Assert.IsTrue(((ControlPersistenceData)controlState).ChildrenActive);
            CollectionAssert.AreEqual(new[] { "wf-one" }, (await store.GetRunnableInstances(now)).ToArray());

            var subscription = new EventSubscription
            {
                Id = "sub-one", WorkflowId = "wf-one", StepId = 3,
                EventName = "approval", EventKey = "row", SubscribeAsOf = now.AddMinutes(-1)
            };
            await store.CreateEventSubscription(subscription);
            loaded.Description = "must roll back";
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => store.PersistWorkflow(loaded, [subscription]));
            Assert.AreNotEqual("must roll back", (await store.GetWorkflowInstance("wf-one")).Description);

            var winners = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
                store.SetSubscriptionToken("sub-one", "token-" + i, "worker-" + i, now.AddMinutes(1))));
            Assert.AreEqual(1, winners.Count(x => x));
            var winner = await store.GetSubscription("sub-one");
            await store.ClearSubscriptionToken("sub-one", "wrong-token");
            Assert.AreEqual(winner.ExternalToken, (await store.GetSubscription("sub-one")).ExternalToken);
            await store.ClearSubscriptionToken("sub-one", winner.ExternalToken);
            Assert.IsNull((await store.GetSubscription("sub-one")).ExternalToken);

            await store.CreateEvent(new Event
            {
                Id = "event-one", EventName = "approval", EventKey = "row", EventTime = now.AddSeconds(-1), EventData = data
            });
            CollectionAssert.AreEqual(new[] { "event-one" }, (await store.GetRunnableEvents(now)).ToArray());
            await store.MarkEventProcessed("event-one");
            Assert.AreEqual(0, (await store.GetRunnableEvents(now)).Count());
            await store.MarkEventUnprocessed("event-one");
            Assert.IsFalse((await store.GetEvent("event-one")).IsProcessed);

            var pending = new ScheduledCommand { CommandName = "resume", Data = "wf-one", ExecuteTime = now.AddSeconds(-1).Ticks };
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.ScheduleCommand(pending)));
            await store.ProcessCommands(now, _ => throw new InvalidOperationException("retry test"));
            await using (var db = new TestContext(options)) Assert.AreEqual(1, await db.ScheduledCommands.CountAsync());
            var executions = 0;
            await store.ProcessCommands(now, _ => { executions++; return Task.CompletedTask; });
            Assert.AreEqual(1, executions);
            await using (var db = new TestContext(options)) Assert.AreEqual(0, await db.ScheduledCommands.CountAsync());

            await store.PersistErrors([new ExecutionError { WorkflowId = "wf-one", ExecutionPointerId = "pointer", ErrorTime = now, Message = "test" }]);
            await store.ClearWorkflowRuntime("wf-one");
            await using (var db = new TestContext(options))
            {
                Assert.AreEqual(0, await db.ExecutionErrors.CountAsync());
                Assert.AreEqual(0, await db.EventSubscriptions.CountAsync());
                Assert.AreEqual(1, await db.WorkflowInstances.CountAsync());
            }
            await store.CreateEventSubscription(subscription);
            var purger = new WorkflowPurger(Create);
            var deleted = await purger.DeleteWorkflowInstancesAsync(["form-row"], ["missing-instance"]);
            CollectionAssert.AreEquivalent(new[] { "wf-one", "missing-instance" }, deleted.ToArray());
            await using (var db = new TestContext(options))
            {
                Assert.AreEqual(0, await db.WorkflowInstances.CountAsync());
                Assert.AreEqual(0, await db.EventSubscriptions.CountAsync());
            }
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static string FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "EIMSNext.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run this test from the EIMSNext solution directory.");
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options), IWfDbContext
    {
        public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
        public DbSet<EventSubscription> EventSubscriptions => Set<EventSubscription>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<ExecutionError> ExecutionErrors => Set<ExecutionError>();
        public DbSet<ScheduledCommand> ScheduledCommands => Set<ScheduledCommand>();
        protected override void OnModelCreating(ModelBuilder builder) => builder.ConfigureWorkflowStore();
    }
}
