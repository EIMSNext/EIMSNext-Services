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
                foreach (var script in ResolveWorkflowStoreScripts(FindSolutionRoot()))
                {
                    var sql = await System.IO.File.ReadAllTextAsync(script);
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

            // 指针拆行：改一个、删一个、加一个，Persist 后按差量落行并完整读回。
            loaded.ExecutionPointers.Single(x => x.Id == "pointer").StepId = 30;
            loaded.ExecutionPointers.Remove(loaded.ExecutionPointers.Single(x => x.Id == "control"));
            loaded.ExecutionPointers.Add(new ExecutionPointer
            {
                Id = "pointer-added", StepId = 9, Active = true,
                PersistenceData = new ControlPersistenceData { ChildrenActive = false }
            });
            await store.PersistWorkflow(loaded);
            var synced = await store.GetWorkflowInstance("wf-one");
            Assert.AreEqual(2, synced.ExecutionPointers.Count);
            Assert.AreEqual(30, synced.ExecutionPointers.Single(x => x.Id == "pointer").StepId);
            var added = synced.ExecutionPointers.Single(x => x.Id == "pointer-added");
            Assert.IsInstanceOfType<ControlPersistenceData>(added.PersistenceData);
            Assert.IsFalse(((ControlPersistenceData)added.PersistenceData).ChildrenActive);
            await using (var db = new TestContext(options))
                Assert.AreEqual(2, await db.ExecutionPointers.CountAsync());
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
                // 实例删除时指针行经外键级联清理。
                Assert.AreEqual(0, await db.ExecutionPointers.CountAsync());
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
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "EIMSNext.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run this test from the EIMSNext solution directory.");
    }

    /// <summary>
    /// 定位 WorkflowCore 存储表与索引脚本。
    /// </summary>
    /// <remarks>
    /// 按<b>版本前缀</b>查找而不是写死文件名：迁移链重排过一次编号
    /// （Workflow 段从 004/005 前移到 003/004），写死的名字在重命名后只会静默失配，
    /// 而这条用例默认因缺少 <c>EIMS_TEST_POSTGRES</c> 而跳过，缺陷可以潜伏很久。
    /// </remarks>
    private static IReadOnlyList<string> ResolveWorkflowStoreScripts(string root)
        => ResolveScripts(root, "003_", "004_");

    private static IReadOnlyList<string> ResolveScripts(string root, params string[] versionPrefixes)
    {
        var directory = Path.Combine(root, "ApiHost", "EIMSNext.Tool.DbMaintenance", "Sql");
        var scripts = new List<string>(versionPrefixes.Length);

        foreach (var prefix in versionPrefixes)
        {
            var matches = Directory.GetFiles(directory, prefix + "*.sql");
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    $"Sql 目录里版本前缀 {prefix} 的脚本应当唯一，实际找到 {matches.Length} 个。");

            scripts.Add(matches[0]);
        }

        return scripts;
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options), IWfDbContext
    {
        public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
        public DbSet<ExecutionPointer> ExecutionPointers => Set<ExecutionPointer>();
        public DbSet<EventSubscription> EventSubscriptions => Set<EventSubscription>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<ExecutionError> ExecutionErrors => Set<ExecutionError>();
        public DbSet<ScheduledCommand> ScheduledCommands => Set<ScheduledCommand>();

        // IWfDbContext 要求把自身收窄为 DbContext（与 WfDbContext 的显式实现一致）。
        DbContext IWfDbContext.Context => this;
        protected override void OnModelCreating(ModelBuilder builder) => builder.ConfigureWorkflowStore();
    }
}
