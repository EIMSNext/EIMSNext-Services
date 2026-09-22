using System.Collections.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Quartz;
using Quartz.Impl;

namespace EIMSNext.Async.Tests;

[TestClass]
public sealed class QuartzPostgreSqlStoreTests
{
    [TestMethod]
    public async Task Store_RetainsJobAndTriggerAcrossRestart()
    {
        var connectionString = Environment.GetEnvironmentVariable("EIMS_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive("Set EIMS_TEST_POSTGRES to a PostgreSQL test connection.");
            return;
        }

        var schema = "quartz_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await command.ExecuteNonQueryAsync();
        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false };
            await using (var setup = new NpgsqlConnection(settings.ConnectionString))
            {
                await setup.OpenAsync();
                foreach (var script in ResolveQuartzStoreScripts(FindSolutionRoot()))
                {
                    var sql = await File.ReadAllTextAsync(script);
                    await using var command = new NpgsqlCommand(sql, setup);
                    await command.ExecuteNonQueryAsync();
                }
            }

            var schedulerName = "PersistenceTest_" + Guid.NewGuid().ToString("N");
            var jobKey = new JobKey("PersistentJob", "Regression");
            var triggerKey = new TriggerKey("PersistentTrigger", "Regression");
            var first = await CreateScheduler(settings.ConnectionString, schedulerName);
            try
            {
                var job = JobBuilder.Create<PersistentNoopJob>().WithIdentity(jobKey)
                    .StoreDurably().UsingJobData("payload", "saved").Build();
                await first.AddJob(job, replace: false);
                await first.ScheduleJob(TriggerBuilder.Create().WithIdentity(triggerKey).ForJob(jobKey)
                    .StartAt(DateTimeOffset.UtcNow.AddDays(1)).Build());
                await first.Start();
            }
            finally { await first.Shutdown(waitForJobsToComplete: true); }

            var second = await CreateScheduler(settings.ConnectionString, schedulerName);
            try
            {
                var reloaded = await second.GetJobDetail(jobKey);
                Assert.IsNotNull(reloaded);
                Assert.AreEqual("saved", reloaded.JobDataMap.GetString("payload"));
                Assert.IsNotNull(await second.GetTrigger(triggerKey));
                await second.Start();
                await using var check = new NpgsqlCommand($"SELECT count(*) FROM \"{schema}\".qrtz_triggers", admin);
                Assert.AreEqual(1L, (long)(await check.ExecuteScalarAsync())!);
            }
            finally { await second.Shutdown(waitForJobsToComplete: true); }
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static Task<IScheduler> CreateScheduler(string connectionString, string name)
    {
        return new StdSchedulerFactory(new NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = name,
            ["quartz.scheduler.instanceId"] = "AUTO",
            ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
            ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.PostgreSQLDelegate, Quartz",
            ["quartz.jobStore.dataSource"] = "default",
            ["quartz.jobStore.tablePrefix"] = "qrtz_",
            ["quartz.dataSource.default.provider"] = "Npgsql",
            ["quartz.dataSource.default.connectionString"] = connectionString,
            ["quartz.serializer.type"] = "newtonsoft"
        }).GetScheduler();
    }

    private static string FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "EIMSNext.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run this test from the EIMSNext solution directory.");
    }

    /// <summary>
    /// 定位 Quartz 存储表与索引脚本。
    /// </summary>
    /// <remarks>
    /// 按<b>版本前缀</b>查找而不是写死文件名：迁移链重排过一次编号
    /// （Quartz 段从 006/007 前移到 005/006），写死的名字在重命名后只会静默失配，
    /// 而这条用例默认因缺少 <c>EIMS_TEST_POSTGRES</c> 而跳过，缺陷可以潜伏很久。
    /// </remarks>
    private static IReadOnlyList<string> ResolveQuartzStoreScripts(string root)
        => ResolveScripts(root, "005_", "006_");

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

    public sealed class PersistentNoopJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
