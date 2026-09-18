using EIMSNext.Common;
using EIMSNext.Tool.DbMaintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace EIMSNext.Core.Tests;

[TestClass]
public sealed class PostgreSqlMaintenanceTests
{
    [TestMethod]
    public async Task Scripts_ApplyOnce_RejectChanges_RollBackFailures_AndSerializeRunners()
    {
        var configured = Environment.GetEnvironmentVariable("EIMS_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured))
        {
            Assert.Inconclusive("Set EIMS_TEST_POSTGRES to a PostgreSQL test connection.");
            return;
        }
        var schema = "maintenance_test_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), schema);
        Directory.CreateDirectory(directory);
        await using var admin = new NpgsqlConnection(configured);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var cs = new NpgsqlConnectionStringBuilder(configured) { SearchPath = schema, Pooling = false }.ConnectionString;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DbMaintenance:SqlDirectory"] = directory
            }).Build();
            var runner = new PostgreSqlMaintenanceRunner(Options.Create(new PostgreSqlOptions { ConnectionString = cs }), config);
            var file = Path.Combine(directory, "001_CreateProbe.sql");
            const string initialSql = "CREATE TABLE probe (id bigint PRIMARY KEY); INSERT INTO probe VALUES (1);";
            await File.WriteAllTextAsync(file, initialSql);

            await runner.ApplyAsync(dryRun: true);
            await using (var check = new NpgsqlCommand($"SELECT to_regclass('{schema}.probe') IS NULL", admin))
                Assert.AreEqual(true, await check.ExecuteScalarAsync());

            await Task.WhenAll(runner.ApplyAsync(), runner.ApplyAsync());
            await runner.ApplyAsync(verifyOnly: true);
            await using (var count = new NpgsqlCommand($"SELECT count(*) FROM \"{schema}\".probe", admin))
                Assert.AreEqual(1L, (long)(await count.ExecuteScalarAsync())!);

            await File.WriteAllTextAsync(file, initialSql + " -- modified");
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runner.ApplyAsync());
            await File.WriteAllTextAsync(file, initialSql);

            var second = Path.Combine(directory, "002_Fail.sql");
            await File.WriteAllTextAsync(second, "CREATE TABLE rollback_probe (id bigint); SELECT definitely_missing_function();");
            await Assert.ThrowsExactlyAsync<PostgresException>(() => runner.ApplyAsync());
            await using (var check = new NpgsqlCommand($"SELECT to_regclass('{schema}.rollback_probe') IS NULL", admin))
                Assert.AreEqual(true, await check.ExecuteScalarAsync());
            await using (var count = new NpgsqlCommand($"SELECT count(*) FROM \"{schema}\".\"__DbMaintenanceHistory\"", admin))
                Assert.AreEqual(1L, (long)(await count.ExecuteScalarAsync())!);

            await File.WriteAllTextAsync(second, "-- destructive: true\nDELETE FROM probe;");
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runner.ApplyAsync());
            config["DbMaintenance:AllowDestructiveChanges"] = "true";
            await runner.ApplyAsync();
            await runner.ApplyAsync(verifyOnly: true);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await cleanup.ExecuteNonQueryAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}
