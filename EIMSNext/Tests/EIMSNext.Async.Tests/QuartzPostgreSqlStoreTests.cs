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
                foreach (var script in new[] { "006_CreateQuartzTables.sql", "007_CreateQuartzIndexes.sql" })
                {
                    var sql = await File.ReadAllTextAsync(Path.Combine(FindSolutionRoot(), "ApiHost", "EIMSNext.Tool.DbMaintenance", "Sql", script));
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

    public sealed class PersistentNoopJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }
}
