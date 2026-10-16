using EIMSNext.Common;
using EIMSNext.Persistence.PostgreSql;

using HKH.Mef2.Integration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Quartz;

namespace EIMSNext.Async.Quartz.Jobs
{
    /// <summary>
    /// RANGE 分区预建作业：确保范围分区表（见 <see cref="PostgreSqlBaselineScript"/> 的 Range 登记）
    /// 的当月与下月分区先于数据存在，避免行落入 default 分区导致分区名存实亡。
    /// DDL 幂等（IF NOT EXISTS），由启动一次性触发 + 每月 Cron 双触发驱动。
    /// </summary>
    [DisallowConcurrentExecution]
    public class PartitionMaintainJob : JobBase<PartitionMaintainJob>
    {
        public PartitionMaintainJob(IResolver resolver)
            : base(resolver)
        {
        }

        protected override async Task ExecuteAsync(IJobExecutionContext context)
        {
            var connectionString = Resolver.Resolve<IOptions<PostgreSqlOptions>>().Value.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("PostgreSql:ConnectionString is required.");

            var now = DateTime.UtcNow;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            foreach (var target in new[] { now, now.AddMonths(1) })
            {
                foreach (var table in PostgreSqlBaselineScript.GetRangePartitionTables())
                {
                    await using var command = new NpgsqlCommand(
                        PostgreSqlBaselineScript.RenderRangePartitionDdl(table, target.Year, target.Month), connection);
                    await command.ExecuteNonQueryAsync();
                }
            }

            Logger.LogDebug("Range partitions ensured for {Tables}", string.Join(",", PostgreSqlBaselineScript.GetRangePartitionTables()));
        }
    }
}
